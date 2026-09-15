# CyberDot.Encoding.Base84

An implementation of base84 encoding in C#, using: https://github.com/jedisct1/zig-base84 as a reference.

Base84 is a binary-to-text encoding that uses 84 ASCII characters, all safe for file names
on macOS, Linux and Windows. It works like Base91, and its output is about 25% larger than
the input, against 33% for Base64.

## Usage

```csharp
using CyberDot.Encoding.Base84;

var bytes = System.Text.Encoding.UTF8.GetBytes("hello world");
var encoded = Base84.Encode(bytes);

var decoded = Base84.Decode(encoded); // Output: hello world
```

### Streaming

`ToBase84Transform`/`FromBase84Transform` implement `ICryptoTransform`, so they plug
into `CryptoStream` for streaming encode/decode without buffering the whole payload in
memory. They default to UTF-8, or accept any `Encoding` via the constructor.

```csharp
using System.Security.Cryptography;

using var output = new MemoryStream();
using (var cryptoStream = new CryptoStream(output, new ToBase84Transform(), CryptoStreamMode.Write))
{
    sourceStream.CopyTo(cryptoStream);
}

using var decoded = new MemoryStream();
using (var cryptoStream = new CryptoStream(encodedStream, new FromBase84Transform(), CryptoStreamMode.Read))
{
    cryptoStream.CopyTo(decoded);
}
```

## License

The MIT License (MIT)
