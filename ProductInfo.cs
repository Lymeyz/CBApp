using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace CBApp1
{
    public class ProductInfo
    {
        //public ProductInfo(string id, string base_min_size, string base_max_size, string quote_increment,
        //    string base_increment, string status, string base_currency, string quote_currency)
        //{
        //    try
        //    {
        //        ID = id;
        //        BaseMinSize = double.Parse(base_min_size, new CultureInfo("En-Us"));
        //        BaseMaxSize = double.Parse(base_max_size, new CultureInfo("En-Us"));
        //        QuoteIncrement = double.Parse(quote_increment, new CultureInfo("En-Us"));
        //        BaseIncrement = double.Parse(base_increment, new CultureInfo("En-Us"));
        //        Status = status;
        //        BaseCurrency = base_currency;
        //        QuoteCurrency = quote_currency;

        //        QuotePrecision = DeterminePrecision(quote_increment, QuoteIncrement);
        //        BasePrecision = DeterminePrecision(base_increment, BaseIncrement);
        //    }
        //    catch (Exception e)
        //    {

        //        Console.WriteLine(e.StackTrace);
        //        Console.WriteLine(e.Message);
        //        throw new Exception();
        //    }
        //}

        [JsonConstructor]
        public ProductInfo( string product_id,
                           string quote_increment,
                           string base_increment,
                           string status,
                           string base_name,
                           string quote_name,
                           string base_min_size,
                           string quote_min_size)
        {
            try
            {
                CultureInfo culture = new CultureInfo( "En-Us" );

                ID = product_id;
                QuoteIncrement = double.Parse(quote_increment, culture);
                BaseIncrement = double.Parse( base_increment, culture );
                Status = status;
                BaseCurrency = base_name;
                QuoteCurrency = quote_name;
                BaseMinSize = double.Parse( base_min_size, culture );
                QuoteMinSize = double.Parse( quote_min_size, culture );


                QuotePrecision = DeterminePrecision(quote_increment, QuoteIncrement);
                BasePrecision = DeterminePrecision(base_increment, BaseIncrement);

                BaseMaxSize = double.MaxValue;
            }
            catch (Exception e)
            {

                Console.WriteLine(e.StackTrace);
                Console.WriteLine(e.Message);
                throw new Exception();
            }
            
        }

        public string ID { get; }
        public double BaseMinSize { get; set; }
        public double BaseMaxSize { get; }
        public double QuoteIncrement { get; }
        public double BaseIncrement { get; }
        public string Status { get; }
        public string BaseCurrency { get; }
        public string QuoteCurrency { get; }
        public int QuotePrecision { get; }
        public int BasePrecision { get; }
        public double QuoteMinSize { get; }

        private int DeterminePrecision(string doubleString, double target)
        {
            try
            {
                int count;

                if (doubleString.Split('.').Length>1)
                {
                    count = 1;
                    foreach (var character in doubleString.Split('.')[1])
                    {
                        if (character != '0')
                        {
                            break;
                        }
                        else
                        {
                            count++;
                        }
                    }
                }
                else
                {
                    count = 0;
                }
                
                if (Math.Round(target, count)!=target)
                {
                    throw new Exception(message: "Precision invalid");
                }

                return count;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                Console.WriteLine(e.Message);
                return -1;
            }
        }
    }
}