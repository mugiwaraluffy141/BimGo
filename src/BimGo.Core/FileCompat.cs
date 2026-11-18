namespace BimGo
{
    internal static class FileCompat
    {
        public static void MoveOverwrite(string source, string destination)
        {
#if NETFRAMEWORK
            if (System.IO.File.Exists(destination)) System.IO.File.Replace(source, destination, null);
            else System.IO.File.Move(source, destination);
#else
            System.IO.File.Move(source, destination, overwrite: true);
#endif
        }
    }
}
