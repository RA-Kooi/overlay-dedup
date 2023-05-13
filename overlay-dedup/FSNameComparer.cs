using System.Collections.Generic;
using System.IO;

namespace overlay_dedup;

#nullable disable

public class FSNameComparer<T>: IEqualityComparer<T> where T: FileSystemInfo
{
	public bool Equals(T l, T r)
	{
		return l.Name == r.Name;
	}

	public int GetHashCode(T d)
	{
		return d.Name.GetHashCode();
	}
}
