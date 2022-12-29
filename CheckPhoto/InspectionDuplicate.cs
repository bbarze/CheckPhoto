using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CheckPhoto
{
    public class InspectionDuplicate
    {
        public String FileName { get; set; }
        public String FullFileName1 { get; set; }
        public String FullFileName2 { get; set; }
        public double Similarity { get; set; }
        public String Extension { get; set; }

    }
}
