using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NINA.Core.Utility;

namespace NINA.Photon.Plugin.ASA
{
    internal class POXlist
    {
        public List<POX> POXs { get; set; }

        public POXlist()
        {
            POXs = new List<POX>();
        }

        public void Add(POX pox)
        {
            POXs.Add(pox);
        }

        public void Clear()
        {
            POXs.Clear();
        }

        public void WritePOX(string path)
        {
            NINA.Core.Utility.Logger.Info("Writing POX file to: " + path);
            
            using (System.IO.StreamWriter poxWriter = new System.IO.StreamWriter(path))
            {
                //write number of points
                poxWriter.WriteLine(POXs.Count);

                int cnt = 1;

                foreach (POX pox in POXs)
                {
                    poxWriter.WriteLine($"\"Number {cnt++}\"");
                    poxWriter.WriteLine($"\"'{pox.DateObs}'\"");
                    poxWriter.WriteLine($"\"{pox.TimeObs}\"");
                  
                    poxWriter.WriteLine($"\"{pox.ExpTime.ToString("0.0000000000000000")}\"");
                    poxWriter.WriteLine(pox.TelescopeRA);
                    poxWriter.WriteLine(pox.SolvedRA);
                    poxWriter.WriteLine(pox.TelescopeDec);
                    poxWriter.WriteLine(pox.SolvedDec);
                    poxWriter.WriteLine($"\"{(pox.PierSide == 1 ? -1 : 1)}\"");
                    poxWriter.WriteLine("\"**************************\"");
                }
            }
        }
    }

    internal class POX
    {
    
        
        public int Number { get; set; }
        public string DateObs { get; set; }
        public string TimeObs { get; set; }
        public double ExpTime { get; set; }
        public double TelescopeRA { get; set; }
        public double SolvedRA { get; set; }
        public double TelescopeDec { get; set; }
        public double SolvedDec { get; set; }
        public int PierSide { get; set; }

                

        public POX(int number, string dateObs, double expTime, double objCTRA, double ra, double objCTDec, double dec, int pierSide)
        {
            Number = number;
            DateObs = dateObs;
            TimeObs = DateObs.Substring(14);
            ExpTime = expTime;
            TelescopeRA = objCTRA;
            SolvedRA = ra;
            TelescopeDec = objCTDec;
            SolvedDec = dec;
            PierSide = pierSide;
        }

    }
}
