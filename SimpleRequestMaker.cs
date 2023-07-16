using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RestSharp;
using System.Web.Routing;
using System.Net.Http;

namespace CBApp1
{
    public class SimpleRequestMaker
    {
        public SimpleRequestMaker(string apiEndpoint)
        {
            this.apiEndpoint = apiEndpoint;
        }

        string apiEndpoint;

        public RestResponse SendRequest(string reqPath, Method method)
        {
            var client = new RestClient(apiEndpoint + reqPath);
            var request = new RestRequest("", method);

            request.AddHeader("Accept", "application/json");

            return client.Execute(request);
        }

    }
}
