# MSFragger Real-Time Search C# Client

A reference C# / .NET client for MSFragger's real-time peptide identification service. The client streams MS2 scans to a gRPC-based MSFragger real-time search server and returns peptide hits.

The repository contains two clients that exercise the same `RealTime.proto` 
service in different ways:

- **`ClientSync`** — unary RPC client (one request, one response per scan).
  Targets `net10.0` and uses blocking calls through `Grpc.Net.Client`.
- **`ClientAsync`** — bidirectional streaming RPC client. Targets `net10.0` and
  uses `Grpc.Net.Client`.

Both clients are intended for benchmarking and integration testing of the
MSFragger real-time searching.

## Repository Layout

```
.
├── MSFraggerRealTimeCSharpClient.sln   Visual Studio solution
├── global.json                         .NET SDK pin (10.0.100, later feature bands allowed)
├── Protos/
│   └── RealTime.proto                  gRPC service / message definitions
├── ClientSync/                         Synchronous (unary) client (net10.0)
│   ├── ClientSync.csproj
│   ├── ProgramSync.cs
│   └── Scan.cs
└── ClientAsync/                        Async streaming client (net10.0)
    ├── ClientAsync.csproj
    ├── ProgramAsync.cs
    └── Scan.cs
```

## gRPC Service

Defined in [`Protos/RealTime.proto`](Protos/RealTime.proto):

```
service RealTimeServer {
    rpc RealTimeSearcherAsync (stream ScanRequest) returns (stream ResultReply);
    rpc RealTimeSearcherSync  (ScanRequest)        returns (ResultReply);
}
```

- `ScanRequest` carries one MS2 spectrum (scan number, precursor m/z, charge,
  m/z + intensity arrays, isolation window) — or, in async mode, a poll request
  asking the server to flush results.
- `ResultReply` returns a list of `Hit`s for one scan, including hyperscore,
  expect value, peptide sequence, mass diff, terminal masses, modification
  masses, charge, theoretical m/z, decoy flag, protein list, next-best score,
  matched/total ion counts, matched fragment annotations, and (optional) glycan
  composition / mass / score.
- `Hit.matchedFragments` reports per-fragment m/z, intensity, ion type
  (N-term, C-term, ±H2O, Y-ion, diagnostic), position, charge, and any
  modification mass.

The C# namespace and Java package are pinned to
`edu.umich.andykong.msfragger.realtimesearch`.

## Requirements

- .NET 10 SDK, 10.0.100 or a later 10.0 feature band (`global.json` pins
  `10.0.100` with `rollForward: latestFeature`). Both clients target `net10.0`;
  no .NET Framework targeting pack is needed. Building has been tested on
  Windows.
- A running MSFragger real-time search server reachable at
  `http://localhost:50051` (default; see *Running Against MSFragger* below).
- An mzML file containing MS2 spectra.

NuGet dependencies are restored automatically:

| Package              | ClientSync | ClientAsync |
|----------------------|------------|-------------|
| Google.Protobuf      | 3.36.2     | 3.36.2      |
| Grpc.Net.Client      | 2.84.0     | 2.84.0      |
| Grpc.Tools           | 2.85.0     | 2.85.0      |
| mzLib                | 1.0.595    | 1.0.595     |

mzLib 1.0.595 pulls in TorchSharp and `libtorch-cpu`, so the restored
packages take about 1.6 GB in the NuGet cache and each client's
`bin/Release/net10.0` folder is about 1 GB.

## Configuration

Both programs are configured via constants at the top of `ProgramSync.cs` /
`ProgramAsync.cs`:

| Constant       | Meaning                                                    |
|----------------|------------------------------------------------------------|
| `Target`       | Server endpoint, `http://localhost:50051` in both clients. Keep the `http://` scheme: the server speaks plaintext HTTP/2. |
| `MzmlPath`     | Absolute path to the mzML file to stream.                  |
| `Dev`          | When `true`, writes results to `<MzmlPath>.realtime.tsv`, or to `<MzmlPath>.realtime.<n>.tsv` if that file exists. |
| `TimeT`        | Threshold for counting "slow" scans (sync: ms; async: ns). |

Edit these values to point at your server and input file before building.

## Build & Run

From the repository root:

```powershell
# Restore + build the whole solution
dotnet build MSFraggerRealTimeCSharpClient.sln -c Release

# Run the synchronous client
dotnet run --project ClientSync -c Release

# Run the streaming async client
dotnet run --project ClientAsync -c Release
```

The built programs can also be started directly, for example
`ClientSync\bin\Release\net10.0\ClientSync.exe` on Windows or
`dotnet ClientSync/bin/Release/net10.0/ClientSync.dll` elsewhere. Neither
client takes command-line arguments.

You can also open `MSFraggerRealTimeCSharpClient.sln` in an IDE that supports
.NET 10 and run either project directly.

## Running Against MSFragger

1. Start MSFragger's real-time search server with a parameter file. The
   database, tolerances, enzyme, and modifications are read from the
   parameter file as in a normal search, and no spectral files are given:

   ```powershell
   java -Xmx32G -jar MSFragger-<version>.jar fragger.params --real-time-search --address localhost --port 50051
   ```

   `--address` and `--port` are optional (defaults `localhost` and `50051`).
   The server builds the fragment index, prints
   `Server started, listening on localhost:50051`, and serves until it is
   stopped with Ctrl+C. The glycan columns are only filled when the parameter
   file sets `glyco_rts = 1`.
2. Set `MzmlPath` (and `Target`, if the address or port differ) and build.
3. Run `ClientSync` or `ClientAsync`. Each loads the MS2 scans of the mzML
   file, sends them to the server, and writes `<MzmlPath>.realtime.tsv`. An
   existing TSV is not overwritten: the results then go to the first free
   `<MzmlPath>.realtime.1.tsv`, `<MzmlPath>.realtime.2.tsv`, and so on, and
   the client prints the path it writes to.

## Output

When `Dev = true`, each client writes a TSV next to the input mzML
(`<MzmlPath>.realtime.tsv`, or `<MzmlPath>.realtime.<n>.tsv` if that file
exists).

`ClientSync` writes the full result schema:

```
scan_num  rank  hyperscore  expect  peptide_sequence  mass_diff
n_term_mass  c_term_mass  mod_masses  charge  theoretical_mz
is_decoy  proteins  next_score  num_match_ions  total_ions
fragments  glycan_composition  glycan_mass  glycan_score
```

`ClientAsync` writes a smaller subset (no charge, theoretical m/z, fragment, or
glycan columns).

The console summary reports total scans searched, wall-clock runtime, average
time per scan, and (sync mode) the number of scans that exceeded `TimeT`.

## How It Works

1. `Prepare(mzmlPath)` loads the mzML file with mzLib and keeps only MS2
   scans, copying their m/z and intensity arrays to `float[]`s along with the
   precursor m/z, charge, and isolation window.
2. **Sync mode** sends one `ScanRequest` per scan and blocks on the matching
   `ResultReply` before moving on.
3. **Async mode** writes every `ScanRequest` to the request stream up front,
   then polls with `Type = 1` requests while a background task drains
   `ResponseStream` until all replies have arrived.
4. Each returned `Hit` is decoded and serialized to the TSV (when `Dev` is on).

## License

Licensed under the [Apache License, Version 2.0](LICENSE).

```
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0
```
