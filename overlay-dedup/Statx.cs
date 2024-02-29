// vim: set ff=unix nobomb ts=4 sw=4
// LICENSE: GPLv3 (See LICENSE in the root of this repository)

using System.Runtime.InteropServices;

internal static class libC
{
[DllImport("libc", SetLastError = true)]
internal static extern int statx(int dirfd, string path, int flags, uint mask, out Statx data);
}

/// <summary>
/// POSIX statx data structure.
/// </summary>
internal struct Statx {

	/// <summary>
	/// Mask of bits indicating filled fields.
	/// </summary>
	internal uint Mask;
	/// <summary>
	/// Block size for filesystem I/O.
	/// </summary>
	internal uint BlockSize;
	/// <summary>
	/// Extra file attribute indicators
	/// </summary>
	internal ulong Attributes;
	/// <summary>
	/// Number of hard links.
	/// </summary>
	internal uint HardLinks;
	/// <summary>
	/// User ID of owner.
	/// </summary>
	internal uint Uid;
	/// <summary>
	/// Group ID of owner.
	/// </summary>
	internal uint Gid;
	/// <summary>
	/// File type and mode.
	/// </summary>
	internal ushort Mode;
	private ushort Padding01;
	/// <summary>
	/// Inode number.
	/// </summary>
	internal ulong Inode;
	/// <summary>
	/// Total size in bytes.
	/// </summary>
	internal ulong Size;
	/// <summary>
	/// Number of 512B blocks allocated.
	/// </summary>
	internal ulong Blocks;
	/// <summary>
	/// Mask to show what's supported in <see cref="Attributes"/>.
	/// </summary>
	internal ulong AttributesMask;
	/// <summary>
	/// Last access time.
	/// </summary>
	internal StatxTimeStamp AccessTime;
	/// <summary>
	/// Creation time.
	/// </summary>
	internal StatxTimeStamp CreationTime;
	/// <summary>
	/// Last status change time.
	/// </summary>
	internal StatxTimeStamp StatusChangeTime;
	/// <summary>
	/// Last modification time.
	/// </summary>
	internal StatxTimeStamp LastModificationTime;
	internal uint RDevIdMajor;
	internal uint RDevIdMinor;
	internal uint DevIdMajor;
	internal uint DevIdMinor;
	internal ulong MountId;
	private ulong Padding02;
	private ulong Padding03;
	private ulong Padding04;
	private ulong Padding05;
	private ulong Padding06;
	private ulong Padding07;
	private ulong Padding08;
	private ulong Padding09;
	private ulong Padding10;
	private ulong Padding11;
	private ulong Padding12;
	private ulong Padding13;
	private ulong Padding14;
	private ulong Padding15;
}

/// <summary>
/// Time stamp structure used by statx.
/// </summary>
public struct StatxTimeStamp {

	/// <summary>
	/// Seconds since the Epoch (UNIX time).
	/// </summary>
	public long Seconds;

	/// <summary>
	/// Nanoseconds since <see cref="Seconds"/>.
	/// </summary>
	public uint Nanoseconds;

}
