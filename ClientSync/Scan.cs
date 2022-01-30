namespace ClientSync
{
    internal class Scan
    {
        public readonly int ScanNum;
        public readonly float PrecursorMz;
        public readonly int Charge;
        public readonly float[] MzArray;
        public readonly float[] IntensityArray;
        public readonly float IsolationWindowLowerMz;
        public readonly float IsolationWindowUpperMz;


        public Scan(int scanNum, float precursorMz, int charge, float[] mzArray, float[] intensityArray, float isolationWindowLowerMz, float isolationWindowUpperMz)
        {
            this.ScanNum = scanNum;
            this.MzArray = mzArray;
            this.PrecursorMz = precursorMz;
            this.Charge = charge;
            this.MzArray = mzArray;
            this.IntensityArray = intensityArray;
            this.IsolationWindowLowerMz = isolationWindowLowerMz;
            this.IsolationWindowUpperMz = isolationWindowUpperMz;
        }
    }
}
