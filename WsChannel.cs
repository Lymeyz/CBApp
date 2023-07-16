using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CBApp1
{
    public class WsChannel
    {
        public WsChannel(string name, params string[] product_ids)
        {
            this.name = name;
            this.product_ids = new List<string>();
            foreach (var item in product_ids)
            {
                this.product_ids.Add(item);
            }
        }

        public string name;
        public List<string> product_ids;
    }
}
