using System.Security.Cryptography;
using System.Text;

namespace Tgs.Sms.Abosend;

public class AbosendSmsHasher
{
	public static string ComputeHash(string source)
	{
		var data = MD5.HashData(Encoding.UTF8.GetBytes(source));
		return Convert.ToHexString(data);
	}
}