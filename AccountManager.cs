using Newtonsoft.Json;
using RestSharp;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;

namespace CBApp1
{
    internal class AccountManager
    {
        public AccountManager(ref RequestMaker authReqMaker)
        {
            accounts = new Dictionary<string, Account>();
            FetchAccounts( authReqMaker );
        }

        public async Task<bool> FetchAccounts( RequestMaker authReqMaker)
        {
            try
            {
                string resp = await authReqMaker.SendAuthRequest( $@"api/v3/brokerage/accounts/", "", HttpMethod.Get, "" );

                if ( resp != null )
                {
                    AccountsHolder accounts = JsonConvert.DeserializeObject<AccountsHolder>( resp );

                    foreach( Account account in accounts.Accounts )
                    {
                        Accounts[ account.Currency ] = account;
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                Console.WriteLine(e.Message);
                return false;
            }
        }

        public Dictionary<string,Account> Accounts
        {
            get
            {
                return accounts;
            }
        }

        private Dictionary<string, Account> accounts;
    }
}