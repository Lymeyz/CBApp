namespace CBApp1
{
    public class OrderBounds
    {
        public OrderBounds(double buyPercent, double buyLimit, double sellPercent, double sellLimit, double quoteSize, double buyDiff,
                            double sellDiff)
        {
            BuyPercent = buyPercent;
            BuyLimit = buyLimit;
            SellPercent = sellPercent;
            SellLimit = sellLimit;
            QuoteSize = quoteSize;
            BuyDiff = buyDiff;
            SellDiff = sellDiff;
        }

        public double BuyPercent { get; }
        public double BuyLimit { get; set; }
        public double BuyDiff { get; set; }

        public double SellPercent { get; }
        public double SellLimit { get; }
        public double SellDiff { get; set; }

        public double QuoteSize { get; }
    }
}