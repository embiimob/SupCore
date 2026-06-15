using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SUP.RPCClient
{
    //Courtesy mb300sd Bitcoin.NET
	public partial class CoinRPC
	{
	  	    		
		/// <summary>
		/// Sends a sendmany RPC call.  The outputs are serialised in the exact insertion
		/// order of <paramref name="ToBitcoinAddresses"/>; no shuffling is applied so
		/// the output positions remain stable (required by the Sup!? P2FK protocol).
		/// </summary>
		public string SendMany(string FromAddress, IEnumerable<KeyValuePair<string, decimal>> ToBitcoinAddresses)
		{
			// Build a JObject that preserves the caller's insertion order.
			// Newtonsoft.Json JObject maintains property insertion order, unlike a plain
			// Dictionary which offers no serialisation-order guarantee.
			var amounts = new JObject();
			foreach (var kv in ToBitcoinAddresses)
				amounts[kv.Key] = kv.Value;

			return RpcCall<string>
				(new RPCRequest("sendmany", new Object[] { FromAddress, amounts, 0 }));
		}

        public string DumpPrivKey(string Address)
        {
            return RpcCall<string>
                (new RPCRequest("dumpprivkey", new Object[] { Address }));
        }

        public List<GetRawDataTransactionResponse> SearchRawDataTransaction(string Address, int Verbose = 0, int skip = 0, int returnQty = 100)
        {
            return RpcCall<List<GetRawDataTransactionResponse>>
                (new RPCRequest("searchrawtransactions", new Object[] { Address, Verbose, skip,returnQty }));
        }


        public GetRawDataTransactionResponse GetRawDataTransaction(string txID, int Verbose = 0)
        {
            return RpcCall<GetRawDataTransactionResponse>
                (new RPCRequest("getrawtransaction", new Object[] { txID, Verbose }));
        }





    }
    public class GetRawDataTransactionResponse
    {
        public class Input
        {
            public class ScriptSig
            {
                public string asm;
                public string hex;
            }

            public string txid;
            public int vout;
            public ScriptSig scriptSig;
            public long sequence;
        }

        public class Output
        {
            public class ScriptPubKey
            {
                public string asm;
                public string hex;
                public int reqSigs;
                public string type;
                public string[] addresses;
            }

            public decimal value;
            public int n;
            public ScriptPubKey scriptPubKey;

        }

        public string hex;
        public string txid;
        public int size;
        public int version;
        public int locktime;
        public string data;
        public Input[] vin;
        public Output[] vout;
        public string blockhash;
        public int confirmations;
        public long blocktime;

        public static implicit operator GetRawDataTransactionResponse(String s)
        {
            return new GetRawDataTransactionResponse() { hex = s };
        }
    }
}
