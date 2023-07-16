using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    class ProfCalc
    {
        public ProfCalc(double buyP, double sellP, double size)
        {
            
            Profit = size*(((199 * sellP) / 200) - ((201 * buyP) / 200));
        }

        public double Profit { get; set; }

    }
}
