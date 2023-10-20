using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RestSharp;
using System.IO;
using Newtonsoft.Json;
using System.Globalization;
using System.Threading;

namespace CBApp1
{
    public class InfoFetcher
    {
        //public InfoFetcher(ref RequestMaker reqMaker, string filename)
        //{
        //    this.reqMaker = reqMaker;
        //    this.filename = filename;
        //}

        public InfoFetcher(ref RequestMaker req)
        {
            this.reqMaker = req;
        }

        private void FetchAndWrite(string productId)
        {
            FetchProductInfo(productId);
            WriteToFile();
        }

        public ProductInfo GetProductInfo(string productId)
        {
            try
            {
                infoString = FetchProductInfo(productId); 
                ProductInfo productInfo;
                //MinSizeHolder minHolder;

                productInfo = JsonConvert.DeserializeObject<ProductInfo>(infoString);

                //base currency ---->___-
                //quote currency -___<---
                //infoString = FetchCurrencyMinSize(productId.Split('-')[0]);
                //minHolder = JsonConvert.DeserializeObject<MinSizeHolder>(infoString);
                //productInfo.BaseMinSize = double.Parse(minHolder.Min_Size, new CultureInfo("En-Us"));

                if( productInfo != null )
                {
                    return productInfo;
                }
                else
                {
                    Console.WriteLine( $"{productId} productInfo null" );
                    return null;
                }
               
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                Console.WriteLine(e.Message);
                return null;
            }
        }

        private string FetchProductInfo(string productId)
        {
            RestResponse resp = null;

            resp = reqMaker.SendAuthRequest( $@"api/v3/brokerage/products/{productId}",
                                                         Method.Get,
                                                         "" );

            return resp.Content;
        }

        private string FetchCurrencyMinSize(string currencyId)
        {
            return reqMaker.SendRequest($"currencies/{currencyId}", Method.Get).Content;
        }

        private void WriteToFile()
        {
            try
            {
                File.WriteAllText(filename, infoString + "\n");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                Console.WriteLine(e.Message);
            }
        }

        private class MinSizeHolder
        {
            public MinSizeHolder(string min_size)
            {
                Min_Size = min_size;
            }

            public string Min_Size { get; }
        }

        private RequestMaker reqMaker;
        private string filename;
        private string infoString;
    }

    
}
