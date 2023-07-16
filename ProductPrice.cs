using System;

namespace CBApp1
{
    public class ProductPrice
    {
        public ProductPrice(string price, string time)
        {
            _price = price;
            _time = time;

            try
            {
                if(_price == null || _time == null)
                {
                    _isComplete = false;
                }
                else
                {
                    _stringDouble = price;
                    _priceDouble = double.Parse(_stringDouble);
                    dateTime = DateTime.Parse(_time);
                    dateTime = dateTime.AddHours(-1);
                    _isComplete = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
        }

        //public ProductPrice(double price1, DateTime time1)
        //{
        //    _priceDouble = price1;
        //    dateTime = time1;
        //    _isComplete = true;
        //    _price = "";
        //    _time = "";
        //}

        public bool isComplete
        {
            get
            {
                return _isComplete;
            }
        }
        public double PriceDouble
        {
            get
            {
                return _priceDouble;
            }
            set
            {
                _priceDouble = value;
            }
        }

        public DateTime dTime
        {
            get
            {
                return dateTime;
            }
            set
            {
                dateTime = value;
            }
        }

        public string stringTime
        {
            get
            {
                return _time;
            }
        }

        public string stringDouble
        {
            get
            {
                return _stringDouble;
            }
        }

        private bool _isComplete;
        private string _stringDouble;
        private string _price;
        private string _time;
        private double _priceDouble;
        private DateTime dateTime;
    }
}