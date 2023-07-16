using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RestSharp;
using RestSharp.Authenticators;
using System.Text.RegularExpressions;

namespace CBApp1
{
    class AuthenticatorAndRequestTests
    {
        public AuthenticatorAndRequestTests()
        {
            // ===========  authenticator test ===========
            Console.WriteLine("Testing authenticator...");
            Authenticator aut = new Authenticator(
                "6cd7ad51b2cdfb9bda7e82d2f618ef0a",
                "hcb9i472w",
                "CSIONywMXj7/guRsOSxPqN24qNJGKpYz25QW74EtjuzaQJAXJZpRFrnxdVTQ0BKi95/totqzIxlybYz4A+mhNg==");

            int unixtime =
                (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

            string[] headers = aut.GenerateHeaders(unixtime, "GET",
                "/accounts", "");

            foreach (var header in headers)
            {
                Console.WriteLine(header);
            }
            Console.WriteLine("Authenticator test done.");
            // ===========  authenticator test ===========

            // =========== send request / recieve response test ===========
            Console.WriteLine("Sending request...");
            var client = new RestClient(@"https://api-public.sandbox.exchange.coinbase.com/accounts");
            var request = new RestRequest(Method.Get.ToString());
            request.AddHeader("cb-access-key", headers[0]);
            request.AddHeader("cb-access-passphrase", headers[1]);
            request.AddHeader("cb-access-sign", headers[2]);
            request.AddHeader("cb-access-timestamp", headers[3]);
            request.AddHeader("Accept", "application/json");

            RestResponse response = client.Execute(request);



            if (response.IsSuccessful)
            {
                Console.WriteLine("Request successful");
                Console.WriteLine("Printing request...");
                Console.WriteLine("");
                Console.WriteLine(response.Content);

            }
            else
            {
                Console.WriteLine("Request failed");
            }
        }
    }
}
