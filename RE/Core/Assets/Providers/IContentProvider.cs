using System.Text;
using Serilog;

namespace RE.Core.Assets.Providers
{
    /// <summary>
    /// Represents methods to access different game assets and other data.
    /// </summary>
    public interface IContentProvider
    {
        /// <summary>
        /// Reads a file from <paramref name="offset"/> to <paramref name="offset"/>+<paramref name="count"/> and returns its content as a byte array.
        /// </summary>
        /// <param name="path">Virtual path to file.</param>
        /// <param name="offset">Start offset.</param>
        /// <param name="count">How many bytes needs to be read.</param>
        /// <returns>Content of a file as a byte array.</returns>
        public byte[] GetBytes(string path, int offset, int count);

        /// <summary>
        /// Reads a file and returns its content as a byte array.
        /// </summary>
        /// <param name="path">Virtual path to file.</param>
        /// <returns>Content of a file as a byte array.</returns>
        public byte[] GetBytes(string path);

        /// <summary>
        /// Reads a file and returns its content as a UTF-8 string.
        /// </summary>
        /// <param name="path">Virtual path to file.</param>
        /// <returns>Content of a file as a <see langword="string"/>.</returns>
        public string GetString(string path)
        {
            var bytes = GetBytes(path);
            if (bytes is [0xEF, 0xBB, 0xBF, ..]) // BOM
            {
                bytes = bytes[3..];
                Log.Verbose("Removed BOM in {Path}", path);
            }

            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Determines whether specified file exists.
        /// </summary>
        /// <param name="path">Virtual path to file.</param>
        /// <returns><see langword="true"/> if file exists, otherwise <see langword="false"/></returns>
        public bool Exists(string path);

        /// <summary>
        /// Determines whether specified directory exists.
        /// </summary>
        /// <param name="path">Virtual path to directory.</param>
        /// <returns><see langword="true"/> if file directory, otherwise <see langword="false"/></returns>
        public bool DirectoryExists(string path);

        /// <summary>Opens an existing file for reading.</summary>
        /// <param name="path">Virtual path to file.</param>
        /// <returns>A <see cref="Stream" /> for the specified path.</returns>
        public Stream Open(string path);

        /// <summary>
        /// Retrieves the names of files in the specified directory.
        /// </summary>
        /// <param name="path">The path to the directory to search.</param>
        /// <param name="recursive"><see langword="true"/> to search all subdirectories of the specified directory; otherwise, <see langword="true"/> to search only the top-level
        /// directory. The default is <see langword="false"/>.</param>
        /// <returns>An array of strings containing the full paths of the files found in the specified directory.</returns>
        public string[] GetFiles(string path, bool recursive = false);

        /// <summary>
        /// Retrieves the names of directories in the specified directory.
        /// </summary>
        /// <param name="path">The path to the directory to search.</param>
        /// <param name="recursive"><see langword="true"/> to search all subdirectories of the specified directory; otherwise, <see langword="false"/> to search only the top-level
        /// directory. The default is <see langword="false"/>.</param>
        /// <returns>An array of strings containing the full paths of the directories found in the specified directory.</returns>
        public string[] GetDirectories(string path, bool recursive = false);

        /// <summary>
        /// Optional method called when the provider is registered in <see cref="ContentManager"/>.
        /// </summary>
        public void Register()
        {
        }

        /// <summary>
        /// Gets the prefix associated with this instance.
        /// </summary>
        /// <remarks>
        /// This prefix is used to identify and select the appropriate content provider for a given asset path.
        /// </remarks>
        public string Prefix { get; }
    }
}