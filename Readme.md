DieFledermaus and DiefledermauZ (.maus file)
============================================
Version 0.2.18.0
---------------
The DieFledermaus file format is simply a [DEFLATE](https://en.wikipedia.org/wiki/DEFLATE)- or [LZMA](https://en.wikipedia.org/wiki/Lempel%E2%80%93Ziv%E2%80%93Markov_chain_algorithm)-compressed file, with metadata and a magic number; it has been extended with the DieFledermauZ [archive file](https://en.wikipedia.org/wiki/Archive_file) format, which can contain multiple files. The names exist solely to be a bilingual pun. The file formats are specified in [DieFledermaus Format.md](DieFledermaus Format.md) and [DieFledermauZ Format.md](DieFledermauZ Format.md).

The project has two components: the DieFledermaus class library, which is intended for programmers, and which allows the full range of capabilities when creating and consuming DieFledermaus and DieFledermauZ files; and the DieFlede command-line utility, which is intended for end users, and which is meant to more accurately reflect the "best practices" when encoding DieFledermaus and DieFledermauZ files.

**Note:** The DieFledermaus and DieFledermauZ formats do not support setting file-permissions, because it's just too obnoxious to try to figure out how to do it in Windows. Therefore, neither format is recommended for backing up file systems.

Features
--------
* DEFLATE compression, LZMA compression, or no compression.
* Storing the filename, creation time, and modified time of the original file.
* Comments on individual files, or in an entire archive in DieFledermauZ.
* Encryption using AES, Twofish, or Threefish, using a password. This includes encrypting any of the above.
* Error checking and encryption verification using [SHA-2](https://en.wikipedia.org/wiki/SHA-2), [SHA-3](https://en.wikipedia.org/wiki/SHA-3), or [Whirlpool](https://en.wikipedia.org/wiki/Whirlpool_%28cryptography%29).

DieFlede
========
A command-line utility for creating DieFledermaus files, using the DieFledermaus class library. The name is an even worse pun.

Usage:
------
Compress files into an archive:
```
DieFled.exe -cf archive.maus file1 file2 file3
```

Decompress files:
```
DieFled.exe -xf archive.maus -o [output directory]
```

List files verbosely:
```
DieFled.exe -lvf archive.maus
```

Show extended help:
```
DieFled.exe --help
```

On Unix and OSX systems, use `mono DieFlede.exe`. Make sure you have [Mono](http://www.mono-project.com/) installed.

Class Library
=============
The DieFledermaus library contains a public type, `DieFledermaus.DieFledermausStream`, which provides much the same functionality as the [`System.IO.Compression.DeflateStream`](https://msdn.microsoft.com/en-us/library/system.io.compression.deflatestream.aspx) or [`System.IO.Compression.GZipStream`](https://msdn.microsoft.com/en-us/library/system.io.compression.gzipstream.aspx) classes. Unlike either of these classes, however, DieFledermausStream must read part of the underlying stream before [`Stream.Read()`](https://msdn.microsoft.com/en-us/library/system.io.stream.read%28v=vs.110%29.aspx) is called.

It also contains `DieFledermaus.DieFledermauZArchive`, which is more or less modelled after [`System.IO.Compression.ZipArchive`](https://msdn.microsoft.com/en-us/library/system.io.compression.ziparchive.aspx).

The DieFledermaus library computes SHA-3 data using the [C# Bouncy Castle library](http://www.bouncycastle.org/). For information on third-party licenses, see [LICENSE-ThirdParty.md](LICENSE-ThirdParty.md).

Disclaimer
==========
**This project is currently in-development. Features and functionality may be changed or removed without notice.**
