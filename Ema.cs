using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    public class Ema : ITimeBound
    {
        public Ema(int length, double price, DateTime time)
        {
            Length = length;
            Price = price;
            Time = time;
        }
        public int Length { get; }
        public double Price { get; set; }
        public DateTime Time { get; }

        public static bool operator <(Ema a, Ema b) => a.Price < b.Price;
        public static bool operator <=(Ema a, Ema b) => a.Price <= b.Price;
        public static bool operator >=(Ema a, Ema b) => a.Price >= b.Price;
        public static bool operator >(Ema a, Ema b) => a.Price < b.Price;
        public static double operator +(Ema a, Ema b) => a.Price + b.Price;
        public static double operator -(Ema a, Ema b) => a.Price - b.Price;
        public static bool operator <( Ema a, double b ) => a.Price < b;
        public static bool operator <=( Ema a, double b ) => a.Price <= b;
        public static bool operator >=( Ema a, double b ) => a.Price >= b;
        public static bool operator >( Ema a, double b ) => a.Price < b;
        public static double operator *( Ema a, double b ) => a.Price * b;
        public static double operator *( double a, Ema b  ) =>  a * b.Price;
        public static double operator -( Ema a, double b ) => a.Price - b;
        public static double operator -( double a, Ema b  ) =>  a - b.Price;
    }

    
}
