using Newtonsoft.Json;
using RestSharp;
using System.Collections.Generic;
using System;
using System.Linq;

namespace CBApp1
{
    internal class AccountManager
    {
        public AccountManager(ref RequestMaker authReqMaker)
        {
            accounts = new Dictionary<string, Account>();
            FetchAccounts(ref authReqMaker);
        }

        public bool FetchAccounts(ref RequestMaker authReqMaker)
        {
            try
            {
                RestResponse resp = authReqMaker.SendAuthRequest( $@"api/v3/brokerage/accounts/", Method.Get, "" );

                if (resp.IsSuccessful)
                {
                    AccountsHolder accounts = JsonConvert.DeserializeObject<AccountsHolder>( resp.Content );

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