using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CBApp1
{
    public class Profile
    {
        [JsonConstructor]
        public Profile(string id, string user_id, string name, string active, string created_at)
        {
            Id = id;
            User_Id = user_id;
            Name = name;
            Active = active;
            Created_At = created_at;
        }

        public string Id { get; }
        public string User_Id { get; }
        public string Name { get; }
        public string Active { get; }
        public string Created_At { get; }
    }
}
