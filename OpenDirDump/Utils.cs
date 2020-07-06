using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace OpenDirDump
{
    class Utils
    {
        private static string[][] HTML_ESCAPE_CODES = new string[][] {
            new string[] { "&", "&amp;" },
            new string[] { "<", "&lt;" },
            new string[] { ">", "&gt;" },
            new string[] { "\"", "&quot;" },
            new string[] { "'", "&apos;" },
        };

        public static string UrlDecode(string input)
        {
            List<byte> bytes = new List<byte>();

            for (int i=0; i<input.Length; i++)
            {
                if (input[i] == '%')
                {
                    string hex = "";
                    if (input[i + 1] == '0')
                    {
                        hex = new String(new char[] { input[i + 2] });
                    } else
                    {
                        hex = new String(new char[] { input[i + 1], input[i + 2] });
                    }
                    bytes.Add((byte)Convert.ToInt64(hex, 16));
                    i += 2;
                } else
                {
                    bytes.Add(Encoding.UTF8.GetBytes(input[i].ToString())[0]);
                }
            }

            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        public static string UrlEncode(string input)
        {
            string output = "";
            char[] ignored = "1234567890abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ-_#=&?~@./;".ToCharArray();
            for (int i = 0; i < input.Length; i++)
            {
                if (!ignored.Contains(input[i]))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(input[i].ToString());
                    foreach (byte b in bytes)
                    {
                        output += "%" + string.Format("{0:x}", b).ToUpper();
                    }
                } else
                {
                    output += input[i];
                }
            }

            return output;
        }

        public static string EscapeHtml(string input)
        {
            foreach (string[] CODE in HTML_ESCAPE_CODES)
            {
                input = input.Replace(CODE[0], CODE[1]);
            }

            return input;
        }

        public static string UnescapeHtml(string input)
        {
            foreach (string[] CODE in HTML_ESCAPE_CODES)
            {
                input = input.Replace(CODE[1], CODE[0]);
            }

            return input;
        }
    }
}
