using System;
using System.Collections.Generic;
using System.Text;

namespace QBMS.Application.Models
{
    public class QuestionQuotaViewModel
    {
        public int EasyRequired { get; set; }
        public int MediumRequired { get; set; }
        public int HardRequired { get; set; }
        public int EasyCreated { get; set; }
        public int MediumCreated { get; set; }
        public int HardCreated { get; set; }
        public int EasyRemaining { get { return EasyRequired - EasyCreated; } }
        public int MediumRemaining { get { return MediumRequired - MediumCreated; } }
        public int HardRemaining { get { return HardRequired - HardCreated; } }

        ///newly added
        public int EasyAssigned { get; set; }

        public int MediumAssigned { get; set; }

        public int HardAssigned { get; set; }

        public int EasyUsed { get; set; }

        public int MediumUsed { get; set; }

        public int HardUsed { get; set; }

        public int Easyremaining { get; set; }

        public int Mediumremaining { get; set; }

        public int Hardremaining { get; set; }

        public int TotalAssigned
        {
            get
            {
                return EasyAssigned
                     + MediumAssigned
                     + HardAssigned;
            }
        }

        public int TotalUsed
        {
            get
            {
                return EasyUsed
                     + MediumUsed
                     + HardUsed;
            }
        }

        public int TotalRemaining
        {
            get
            {
                return Easyremaining
                     + Mediumremaining
                     + Hardremaining;
            }
        }


    }

}
