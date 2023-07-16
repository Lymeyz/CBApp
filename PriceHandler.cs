using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    public class PriceHandler
    {
        public PriceHandler(string product_id)
        {
            lastPrices = new Queue<ProductPrice>(3700);
            currentPrices = new Stack<ProductPrice>(500);
            _currentPrices = new Stack<ProductPrice>(500);
            _prices = new Queue<ProductPrice>(3700);

        }

        private Queue<ProductPrice> lastPrices;
        private Stack<ProductPrice> currentPrices;
        private Stack<ProductPrice> _currentPrices;
        private Queue<ProductPrice> _prices;

        public Queue<ProductPrice> Prices 
        {
            get
            {
                return _prices;
            }
        }

        public double TryPeekLastPrice()
        {
            try
            {
                if (_currentPrices.Count!=0 && _currentPrices!=null)
                {
                    return _currentPrices.Peek().PriceDouble;
                }
                else
                {
                    return -1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return -1;
            }
        }

        public bool TryCopyPrices(int minPrices)
        {
            try
            {
                if (lastPrices.Count>=minPrices)
                {
                    _prices = new Queue<ProductPrice>(lastPrices);

                    return true;
                }
                else
                {
                    Console.WriteLine("Too few prices");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }

        }

        public bool TryEnqueue(ProductPrice price)
        {
            try
            {
                //check if price is a valid ProductPrice 
                if (!price.isComplete)
                {
                    return false;
                }

                //Console.WriteLine(price.PriceDouble + " ----- " + price.dTime);

                // new stack, new minute (prev second)
                if (currentPrices.Count==0)
                {
                    //Console.WriteLine("new stack");
                    currentPrices.Push(price);
                    _currentPrices = new Stack<ProductPrice>(currentPrices);

                    return false;
                }
                // one ProductPrice in stack, new ProductPrice with "same" timestamp
                else if(price.dTime.Hour == currentPrices.Peek().dTime.Hour &&
                    price.dTime.Minute == currentPrices.Peek().dTime.Minute)
                {
                    if (currentPrices.Peek().PriceDouble!=price.PriceDouble)
                    {
                        currentPrices.Push(price);
                    }
                    _currentPrices = new Stack<ProductPrice>(currentPrices);

                    return false;
                }
                // new timestamp
                else
                {
                    // new timestamp, only 1 price recieved
                    if (currentPrices.Count == 1)
                    {
                        if (lastPrices.Count > 3650)
                        {
                            lastPrices.Dequeue();
                        }

                        lastPrices.Enqueue(currentPrices.Pop());
                        currentPrices.Push(price);
                    }
                    // new timestamp, more than 1 price recieved
                    else
                    {
                        ProductPrice highPrice =
                        new ProductPrice(((double)-1).ToString(), currentPrices.Peek().stringTime);

                        ProductPrice lowPrice =
                            new ProductPrice(((double)99999999999999999).ToString(), currentPrices.Peek().stringTime);

                        ProductPrice tempPrice;

                        while (currentPrices.Count != 0)
                        {
                            tempPrice = currentPrices.Pop();

                            if (tempPrice.PriceDouble > highPrice.PriceDouble)
                            {
                                highPrice = new ProductPrice(tempPrice.stringDouble, tempPrice.stringTime);
                                highPrice.PriceDouble = tempPrice.PriceDouble;
                                highPrice.dTime = tempPrice.dTime;
                            }
                            if (tempPrice.PriceDouble < lowPrice.PriceDouble)
                            {
                                lowPrice = new ProductPrice(tempPrice.stringDouble, tempPrice.stringTime);
                                lowPrice.PriceDouble = tempPrice.PriceDouble;
                                lowPrice.dTime = tempPrice.dTime;
                            }
                        }

                        currentPrices.Push(price);

                        if (highPrice.PriceDouble!=lowPrice.PriceDouble)
                        {
                            if (lastPrices.Count > 3650)
                            {
                                lastPrices.Dequeue();
                                lastPrices.Dequeue();
                            }
                            lastPrices.Enqueue(highPrice);
                            lastPrices.Enqueue(lowPrice);
                            //Console.Write("2 Prices added: ");
                            //Console.WriteLine($"High price: {highPrice.PriceDouble} Low price: {lowPrice.PriceDouble}");
                        }
                        else
                        {
                            //Console.WriteLine($"Single price added: {highPrice.PriceDouble} ------- {highPrice.dTime}");
                            if (lastPrices.Count > 3650)
                            {
                                lastPrices.Dequeue();
                            }
                            lastPrices.Enqueue(highPrice);
                        }
                        
                    }
                    //Console.WriteLine($"In Queue: {lastPrices.Count}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.Data);
                return false;
            }
        }
    }
}
