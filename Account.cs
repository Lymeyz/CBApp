using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace CBApp1
{
    public class Account
    {
        [JsonConstructor]
        public Account( string currency,
                       AvailableBalance available_balance,
                       string type,
                       string active,
                       string ready,
                       Hold hold)
        {
            Currency = currency;
            this.balance = available_balance;
            Type = type;
            Active = active;
            Ready = ready;
            this.hold = hold;
        }

        public string Type { get; }
        public string Active { get; }
        public string Ready { get; }
        public double BalanceDouble
        {
            get
            {
                return balance.Value;
            }
        }
        public double AvailableBalance
        {
            get
            {
                return balance.Value;
            }
        }
        public double Hold
        {
            get
            {
                return hold.Value;
            }
        }
        public string Currency { get; }


        private Hold hold;
        private AvailableBalance balance;
    }

    public class AccountsHolder
    {
        public AccountsHolder( Account[] accounts )
        {
            Accounts = accounts;
        }

        public Account[] Accounts { get; }
    }

    public class AvailableBalance
    {
        public AvailableBalance(string value)
        {
            Value = double.Parse( value, new CultureInfo( "En-Us" ) );
        }

        public double Value { get; }
    }

    public class Hold
    {
        public Hold( string value)
        {
            Value = double.Parse( value, new CultureInfo( "En-Us" ) );
        }

        public double Value { get; }
    }
}
