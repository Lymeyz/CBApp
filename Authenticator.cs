using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Net.Mime;

namespace CBApp1
{
    /*
        Contains the API-key, passphrase, secret key, and a method for signing
        requests along with related helper-methods.
        */

    public class Authenticator
    {
        public Authenticator(string accessKey, string passPhrase, string secretKey)
        {
            this.accessKey = accessKey;
            this.passPhrase = passPhrase;
            this.secretKey = secretKey;

            MakeHMAC();
        }

        public Authenticator( Authenticator auth)
        {
            this.accessKey = auth.accessKey;
            this.passPhrase = auth.passPhrase;
            this.secretKey = auth.secretKey;
            this.reqHmac = auth.Hmac;
        }

        public string[] GenerateHeaders(int time, string method,
            string requestPath, string body)
        {

            string[] headers = new string[3];

            // CB-ACCESS-KEY
            headers[0] = accessKey;
            // CB-ACCESS-SIGN
            headers[1] = GenerateSign(MakePreHash(time, method.ToString().ToUpper(), requestPath, body));
            // CB-ACCESS-TIMESTAMP
            headers[2] = time.ToString();


            return headers;
        }

        public string[] GenerateWsHeaders(string channelName, string[] products)
        {
            string[] headers = new string[ 4 ];

            headers[ 0 ] = channelName;
            headers[ 1 ] = accessKey;
            headers[ 2 ] = GetUnixTime().ToString();
            headers[ 3 ] = GenerateWsSign( MakeWsPreHash( headers[ 2 ], headers[ 0 ], products ) );
            int length = headers[ 3 ].Length;

            return headers;
        }

        // step 1: make the pre-hash string
        // Builds pre-hash string
        private string MakePreHash(int time, string method, string requestPath,
            string body)
        {
            //return time.ToString() + method + "/" + requestPath + body;
            return time.ToString() + method + requestPath + body;
        }
        
        // Step 2, 3 - decode secretkey and make SHA256-HMAC using the key.
        // Creates SHA256-HMAC with decoded secret key
        // ACTUALLY STEP 1.

        private string MakeWsPreHash(string time, string channelName, string[] products)
        {
            string productsString = "";

            foreach( var product in products )
            {
                productsString += $"{product},";
            }

            if( productsString.Length > 0 )
            {
                productsString = productsString.TrimEnd( ',' );
            }
            
            return $"{time}{channelName}{productsString}";
        }
        private void MakeHMAC()
        {
            //reqHmac = new HMACSHA256( Convert.FromBase64String( secretKey ) );

            byte[] keyBytes = Encoding.UTF8.GetBytes( secretKey );

            reqHmac = new HMACSHA256( keyBytes );
        }

        //step 4 - sign message with HMAC and encode in base64
        private string GenerateSign(string preHash)
        {
            byte[] data = reqHmac.ComputeHash( Encoding.UTF8.GetBytes( preHash ) );

            var sBuilder = new StringBuilder();

            for( int i = 0; i < data.Length; i++ )
            {
                sBuilder.Append( data[ i ].ToString( "x2" ) );
            }

            string hexString2 = sBuilder.ToString();

            byte[] buffer = Encoding.ASCII.GetBytes(preHash);
            reqHmac.ComputeHash(buffer);
            string ret = Convert.ToBase64String( reqHmac.Hash );
            return hexString2;
        }
        
        private string GenerateWsSign(string input)
        {
            byte[] data = reqHmac.ComputeHash( Encoding.UTF8.GetBytes( input ) );

            var sBuilder = new StringBuilder();

            for( int i = 0; i < data.Length; i++ )
            {
                sBuilder.Append( data[ i ].ToString( "x2" ) );
            }

            return sBuilder.ToString();
        }

        private bool VerifySign( string input, string Sign )
        {
            var signOfInput = GenerateWsSign( input );

            StringComparer comparer = StringComparer.OrdinalIgnoreCase;

            return comparer.Compare( signOfInput, Sign ) == 0;
        }

        private string GenerateHexSign(string preHash)
        {
            byte[] buffer = Encoding.ASCII.GetBytes( preHash );
            reqHmac.ComputeHash( buffer );

            string hexSign = "";
            int hexInt;
            string hexChar;

            foreach( char character in reqHmac.Hash )
            {
                hexInt = Convert.ToInt32( character );
                hexChar = $"{hexInt:X}";
                hexSign += hexChar;
            }


            return hexSign;
        }

        public int GetUnixTime()
        {
            return (int)DateTime.UtcNow.Subtract( new DateTime( 1970, 1, 1 ) ).TotalSeconds;
        }

        public HMACSHA256 Hmac
        {
            get
            {
                return reqHmac;
            }
        }

        private readonly string accessKey;
        private readonly string passPhrase;
        private readonly string secretKey;
        private HMACSHA256 reqHmac;
    }
}
