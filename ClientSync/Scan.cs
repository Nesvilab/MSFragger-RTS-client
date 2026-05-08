// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at

//     http://www.apache.org/licenses/LICENSE-2.0

// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
