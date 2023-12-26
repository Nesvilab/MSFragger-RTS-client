using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using edu.umich.andykong.msfragger.realtimesearch;
using Google.Protobuf.Collections;
using Grpc.Core;
using IO.MzML;

namespace ClientSync
{
    public static class ProgramSync
    {
        private static List<Scan> _scanList;
        private const string Target = "localhost:50051";
        private const bool Dev = true;
        private const string MzmlPath = @"G:\dev\msfragger\realtime_search\b1906_293T_proteinID_01A_QE3_122212.mzML";
        // private const string MzmlPath = @"G:\dev\msfragger\realtime_search\20170609_QEh1_LC1_ChCh_FAMA_SA_HLAIIp_JY_210417_2_R1.mzML";
        private const long TimeT = 10000000L;


        public static async Task Main()
        {
            Console.WriteLine("Sync mode.");
            Console.WriteLine($"Loading scans from {MzmlPath}...");
            _scanList = Prepare(MzmlPath);

            Console.WriteLine("Start testing");

            var channel = new Channel(Target, ChannelCredentials.Insecure);
            var client = new RealTimeServer.RealTimeServerClient(channel);

            var startTime = GetNanoseconds();

            StreamWriter streamWriter;
            if (Dev)
            {
                streamWriter = new StreamWriter(MzmlPath + ".realtime.tsv");
                await streamWriter.WriteLineAsync("scan_num\trank\thyperscore\texpect\tpeptide_sequence\tmass_diff\tn_term_mass\tc_term_mass\tmod_masses\tis_decoy\tproteins\tnext_score\tnum_match_ions\ttotal_ions\tfragments");
            }

            var count = 0;
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

                var resultReply = client.RealTimeSearcherSync(scanRequest);

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

                        var nextscore = hit.HasNextscore ? hit.Nextscore : 0;
                        var numMatchedFragments = hit.HasNumMatchedIons ? hit.NumMatchedIons : 0;
                        var numTotalIons = hit.HasNumTotalIons ? hit.NumTotalIons : 0;

                        RepeatedField<Fragment> fragmentList = hit.MatchedFragments;

                        if (Dev)
                        {
                            await streamWriter.WriteLineAsync(scanNum + "\t" + rank + "\t" + hyperscore + "\t" + expect + "\t" + peptideSequence + "\t" + massDiff + "\t" + nTermMass + "\t" + cTermMass + "\t" + string.Join(";", modMasses) + "\t" + isDecoy + "\t" + proteins + "\t" + nextscore + "\t" + numMatchedFragments + "\t" + numTotalIons + "\t" + string.Join(";", fragmentList));
                        }
                    }
                }
                else if (status == 1)
                {
                    Console.WriteLine($"There was an error in searching scan {resultReply.ScanNum}. Error message: {resultReply.Status}");
                }

                var runTime2 = GetNanoseconds() - startTime2;

                if (runTime2 > TimeT)
                {
                    Console.WriteLine($"Scan {resultReply.ScanNum} took more than {TimeT * 1e-6} ms: {Math.Round(runTime2 * 1e-6, 2)} ms");
                    ++count;
                }
            }

            if (Dev)
            {
                streamWriter.Close();
            }

            var runTime = (GetNanoseconds() - startTime) * 1e-6;

            Console.WriteLine($"Searched {_scanList.Count} scans in {runTime} ms");
            Console.WriteLine($"Average speed {Math.Round(runTime / _scanList.Count, 2)} ms");
            Console.WriteLine($"Number of scans taking longer than {TimeT * 1e-6} ms: {count}");
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
                        mzArray[i] = (float) massSpectrum.XArray[i];
                        intensityArray[i] = (float)massSpectrum.YArray[i];
                    }
                    scanList.Add(new Scan(scan.OneBasedScanNumber, (float) scan.SelectedIonMZ, scan.SelectedIonChargeStateGuess.Value, mzArray, intensityArray, (float) scan.IsolationRange.Minimum, (float) scan.IsolationRange.Maximum));
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
