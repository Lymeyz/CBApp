using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    /// <summary>
    /// Preliminary order class
    /// </summary>
    public class PreOrder : ITimeBound
    {
        public PreOrder(string productId, DateTime time, bool b)
        {
            ProductId = productId;
            Time = time;
            B = b;
            Complete = false;
            PeakDiff = -1;
            PeakTime = DateTime.MinValue;
            Complementary = false;
            SellOff = false;
        }

        public PreOrder(PreOrder prelO)
        {
            this.ProductId = prelO.ProductId;
            this.Time = prelO.Time;
            this.B = prelO.B;
            this.Complete = prelO.Complete;
            this.Price = prelO.Price;
            this.PeakDiff = prelO.PeakDiff;
            this.PeakTime = prelO.PeakTime;
            this.Complementary = prelO.Complementary;
            this.SellOff = prelO.SellOff;
        }

        public string ProductId { get; set; }
        // Start of current trend window
        public bool B { get; }
        public bool Complete { get; set; }
        public double Price { get; set; }
        public double PeakDiff { get; set; }
        public DateTime PeakTime { get; set; }
        public DateTime Time { get; set; }
        public double StartPrice { get; set; }
        public double peakPrice { get; set; }
        public double Size { get; set; }
        public bool Complementary { get; set; }
        public bool SellOff { get; set; }
    }
}
