using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using Newtonsoft.Json;

namespace CBApp1
{
    public class MessageReciever
    {
        public MessageReciever(string product_id, ref PriceHandler priceHandler)
        {
            _priceHandler = priceHandler;
        }
        public string Product_Id { get; }
        private PriceHandler _priceHandler;

        private ProductPrice ProcessMessageData(string data)
        {
            try
            {
                ProductPrice tick = JsonConvert.DeserializeObject<ProductPrice>(data);

                _priceHandler.TryEnqueue(tick);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }


            return null;
        }

        // On message, process message and pass pricedata to PriceHandler
        public void Ws_OnMessage(object sender, MessageEventArgs e)
        {
            try
            {
                ProductPrice prz = ProcessMessageData(e.Data);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.StackTrace);
                Console.WriteLine(ex.Data);
            }
        }
    }
}
