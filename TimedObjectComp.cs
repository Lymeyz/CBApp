using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    class TimedObjectComp : IComparer<DateTime>
    {
        public int Compare(DateTime x, DateTime y)
        {
            if (x.CompareTo(y) != 0)
            {
                return x.CompareTo(y);
            }
            else
            {
                return 0;
            }
        }
    }
}
