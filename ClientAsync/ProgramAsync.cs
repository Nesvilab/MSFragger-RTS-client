// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at

//     http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using edu.umich.andykong.msfragger.realtimesearch;
using Grpc.Core;
using Grpc.Net.Client;
using IO.MzML;

namespace ClientAsync
{
    public static class ProgramAsync
    {
        private static List<Scan>? _scanList;
        private const string Target = "http://localhost:50051";
        private const bool Dev = true;
        // private const string MzmlPath = @"F:\dev\msfragger\realtime_search\b1906_293T_proteinID_01A_QE3_122212.mzML";
        private const string MzmlPath = @"F:\dev\msfragger\realtime_search\20170609_QEh1_LC1_ChCh_FAMA_SA_HLAIIp_JY_210417_2_R1.mzML";
        private const long TimeT = 10000000L;


        public static async Task Main()
        {
            Console.WriteLine("Async mode.");
            Console.WriteLine($"Loading scans from {MzmlPath}...");
            _scanList = Prepare(MzmlPath);

            Console.WriteLine("Start testing");

            var channel = GrpcChannel.ForAddress(Target);
            var client = new RealTimeServer.RealTimeServerClient(channel);
            var call = client.RealTimeSearcherAsync();

            var startTime = GetNanoseconds();

            StreamWriter streamWriter;
            if (Dev)
            {
                streamWriter = new StreamWriter(MzmlPath + ".realtime.tsv");
                await streamWriter.WriteLineAsync("scan_num\trank\thyperscore\texpect\tpeptide_sequence\tmass_diff\tn_term_mass\tc_term_mass\tmod_masses\tis_decoy\tproteins");
            }

            // send scans out
            foreach (var scan in _scanList)
            {
                var startTime2 = GetNanoseconds();

                var scanRequest = new ScanRequest
                {
                    Type = 0,
                    ScanNum = scan.ScanNum,
                    PrecursorMz = scan.PrecursorMz,
                    Charge = scan.Charge,
                    Mz = { scan.MzArray },
                    Intensity = { scan.IntensityArray },
                    IsolationWindowLowerMz = scan.IsolationWindowLowerMz,
                    IsolationWindowUpperMz = scan.IsolationWindowUpperMz
                };
                await call.RequestStream.WriteAsync(scanRequest);
            }

            // ask the server to send the result back
            var count = 0;
            var responseTask = Task.Run(async () =>
            {
                await foreach (var resultReply in call.ResponseStream.ReadAllAsync())
                {
                    var status = resultReply.Status;
                    if (status == 0)
                    {
                        var scanNum = resultReply.ScanNum;
                        foreach (var hit in resultReply.Hits)
                        {
                            var rank = hit.Rank;
                            var hyperscore = hit.Hyperscore;
                            var expect = hit.Expect;
                            var peptideSequence = hit.PeptideSequence;
                            var massDiff = hit.MassDiff;
                            var nTermMass = hit.NTermMass;
                            var cTermMass = hit.CTermMass;
                            var isDecoy = hit.IsDecoy;
                            var proteins = hit.Proteins;
                            var modMasses = new float[hit.ModMasses.Count];
                            for (var i = 0; i < modMasses.Length; ++i)
                            {
                                modMasses[i] = hit.ModMasses[i];
                            }

                            if (Dev)
                            {
                                await streamWriter.WriteLineAsync(scanNum + "\t" + rank + "\t" + hyperscore + "\t" + expect + "\t" + peptideSequence + "\t" + massDiff + "\t" + nTermMass + "\t" + cTermMass + "\t" + string.Join(";", modMasses) + "\t" + isDecoy + "\t" + proteins);
                            }
                        }
                    }
                    else if (status == 1)
                    {
                        Console.WriteLine($"There was an error in searching scan {resultReply.ScanNum}. Error message: {resultReply.Status}");
                    }

                    ++count;
                }
            });

            while (count < _scanList.Count)
            {
                var request = new ScanRequest
                {
                    Type = 1
                };
                await call.RequestStream.WriteAsync(request);
                Thread.Sleep(1);
            }

            await call.RequestStream.CompleteAsync();
            await responseTask;

            if (Dev)
            {
                streamWriter.Close();
            }

            var runTime = (GetNanoseconds() - startTime) * 1e-6;

            Console.WriteLine($"Searched {_scanList.Count} scans in {runTime} ms");
            Console.WriteLine($"Average speed {Math.Round(runTime / _scanList.Count, 2)} ms");
            Console.WriteLine("DONE\n");
        }


        private static List<Scan> Prepare(string mzmlPath)
        {
            var mzml = Mzml.LoadAllStaticData(mzmlPath);
            var scanList = new List<Scan>(mzml.NumSpectra);
            foreach (var scan in mzml.GetAllScansList())
            {
                if (scan.MsnOrder == 2)
                {
                    var massSpectrum = scan.MassSpectrum;
                    var mzArray = new float[massSpectrum.Size];
                    var intensityArray = new float[massSpectrum.Size];
                    for (var i = 0; i < massSpectrum.Size; ++i)
                    {
                        mzArray[i] = (float)massSpectrum.XArray[i];
                        intensityArray[i] = (float)massSpectrum.YArray[i];
                    }
                    scanList.Add(new Scan(scan.OneBasedScanNumber, (float)scan.SelectedIonMZ, scan.SelectedIonChargeStateGuess.Value, mzArray, intensityArray, (float)scan.IsolationRange.Minimum, (float)scan.IsolationRange.Maximum));
                }
            }

            return scanList;
        }

        private static long GetNanoseconds()
        {
            var timestamp = Stopwatch.GetTimestamp();
            var nanoseconds = 1e9 * timestamp / Stopwatch.Frequency;

            return (long)nanoseconds;
        }
    }
}
