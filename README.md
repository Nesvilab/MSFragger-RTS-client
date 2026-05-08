# MSFragger Real-Time Search C# Client

A reference C# / .NET client for MSFragger's real-time peptide identification service. The client streams MS2 scans to a gRPC-based MSFragger real-time search server and returns peptide hits.

The repository contains two clients that exercise the same `RealTime.proto` 
service in different ways:

- **`ClientSync`** — unary RPC client (one request, one response per scan).
  Targets `net481` (.NET Framework 4.8.1) and uses `Grpc.Core` (Grpc).
- **`ClientAsync`** — bidirectional streaming RPC client. Targets `net5.0` and
  uses `Grpc.Net.Client`.

Both clients are intended for benchmarking and integration testing of the
MSFragger real-time searching.

## Repository Layout

```
.
├── MSFraggerRealTimeCSharpClient.sln   Visual Studio solution
├── Protos/
│   └── RealTime.proto                  gRPC service / message definitions
├── ClientSync/                         Synchronous (unary) client (net481)
│   ├── ClientSync.csproj
│   ├── ProgramSync.cs
│   └── Scan.cs
└── ClientAsync/                        Async streaming client (net5.0)
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

- Windows with Visual Studio 2022 (or `dotnet` SDK with the relevant targeting
  packs).
- .NET Framework 4.8.1 targeting pack (for `ClientSync`).
- .NET 5.0 SDK (for `ClientAsync`).
- A running MSFragger real-time search server reachable at `localhost:50051`
  (default; see *Configuration* below).
- An mzML file containing MS2 spectra.

NuGet dependencies are restored automatically:

| Package              | ClientSync | ClientAsync |
|----------------------|------------|-------------|
| Google.Protobuf      | 3.24.4     | 3.24.4      |
| Grpc                 | 2.46.6     | —           |
| Grpc.Net.Client      | —          | 2.57.0      |
| Grpc.Tools           | 2.58.0     | 2.58.0      |
| mzLib                | 1.0.465    | 1.0.508     |

## Configuration

Both programs are configured via constants at the top of `ProgramSync.cs` /
`ProgramAsync.cs`:

| Constant       | Meaning                                                    |
|----------------|------------------------------------------------------------|
| `Target`       | Server endpoint. Sync uses `localhost:50051`; Async uses `http://localhost:50051`. |
| `MzmlPath`     | Absolute path to the mzML file to stream.                  |
| `Dev`          | When `true`, writes results to `<MzmlPath>.realtime.tsv`.  |
| `TimeT`        | Threshold for counting "slow" scans (sync: ms; async: ns). |

Edit these values to point at your server and input file before building.

## Build & Run

From the repository root:

```powershell
# Restore + build the whole solution
dotnet build MSFraggerRealTimeCSharpClient.sln -c Release

# Run the synchronous client (net481)
dotnet run --project ClientSync -c Release

# Run the streaming async client (net5.0)
dotnet run --project ClientAsync -c Release
```

You can also open `MSFraggerRealTimeCSharpClient.sln` in Visual Studio and run
either project directly.

## Output

When `Dev = true`, each client writes a TSV next to the input mzML
(`<MzmlPath>.realtime.tsv`).

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
