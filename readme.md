# Com.H.Extensions.Configuration.Xml

A thread-safe library that extends the Microsoft.Extensions.Configuration library to support reading and writing configuration values to XML configuration files.

## Installation
Best way to install this library is via NuGet package manager [Com.H.Extensions.Configuration.Xml](https://www.nuget.org/packages/Com.H.Extensions.Configuration.Xml).

## Example 1
This example demonstrates how to write a value to a configuration file and save it.

To run this sample, you need to:
1) Create a new console application
2) Add NuGet package [Com.H.Extensions.Configuration.Xml](https://www.nuget.org/packages/Com.H.Extensions.Configuration.Xml)  
4) Copy and paste the following code into your Program.cs file:

```csharp
using Microsoft.Extensions.Configuration;
using Com.H.Extensions.Configuration.Xml;

var builder = new ConfigurationBuilder()
    .AddXmlFileWithSave("config.xml", optional: true, reloadOnChange: true);

IConfiguration configuration = builder.Build();

// write a value
configuration["key"] = "test value";

// save the changes
configuration.Save();
```
> Note: the above code will create a new file `config.xml` in the output directory of your console application. The file will contain the following content:
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <key>test value</key>
</configuration>
```

## Example 2
This example demonstrates how to read a value from a configuration file.

```csharp
using Microsoft.Extensions.Configuration;
using Com.H.Extensions.Configuration.Xml;

var builder = new ConfigurationBuilder()
	.AddXmlFileWithSave("config.xml", optional: true, reloadOnChange: true);

IConfiguration configuration = builder.Build();

// read a value
var value = configuration["key"];
Console.WriteLine(value);
```

## Example 3
This example demonstrates how to save a value encapsulated in a CDATA section. This is useful when you want to save a value that contains special characters.

```csharp
using Microsoft.Extensions.Configuration;
using Com.H.Extensions.Configuration.Xml;

var builder = new ConfigurationBuilder()
	.AddXmlFileWithSave("config.xml", optional: true, reloadOnChange: true);

IConfiguration configuration = builder.Build();

// write a value
configuration.SetWithCData("key", "test value with some special characters &>#");

// save the changes
configuration.Save();
```
> **Note**: the above code will create a new file `config.xml` in the output directory of your console application. The file will contain the following content:
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <key><![CDATA[test value with some special characters &>#]]></key>
</configuration>
```

## Example 4
This example demonstrates how to read a value encapsulated in a CDATA section.

```csharp
using Microsoft.Extensions.Configuration;
using Com.H.Extensions.Configuration.Xml;

var builder = new ConfigurationBuilder()
	.AddXmlFileWithSave("config.xml", optional: true, reloadOnChange: true);

IConfiguration configuration = builder.Build();

// read a value
var value = configuration["key"];
Console.WriteLine(value);

// notice that there is no need to do anything special to read a value encapsulated in a CDATA section
// the library will automatically handle it. All you need to to is to read the value as usual

// updating the value
configuration["key"] = "new value with special characters #&>";

// notice that there is no need to do anything special to update a value encapsulated in a CDATA section
// the library will automatically handle it. All you need to to is to update the value as usual.
// You only need to use SetWithCData method when it's the first time you write the value.

// save the changes
configuration.Save();
```
> **Note**: Notice that there was no need to do anything special to read or update a value encapsulated in a CDATA section. The library will automatically handle it if the .xml file contains a value encapsulated in a CDATA section. However, if you are writing a new key to the configuration file that never been written before, you need to use SetWithCData method to encapsulate the value in a CDATA section only once. After that, you can read and update the value as usual.

## Example 5
This example demonstrates how to read a value from a configuration file and the value gets updated outside the application and the application automatically reloads the updated value.

```csharp
using Microsoft.Extensions.Configuration;
using Com.H.Extensions.Configuration.Xml;

var builder = new ConfigurationBuilder()
	.AddXmlFileWithSave("config.xml", optional: true, reloadOnChange: true);

IConfiguration configuration = builder.Build();

bool continueLoop = true;

while (continueLoop)
{
    string settingValue = configuration["key"];
    if (!string.IsNullOrEmpty(settingValue))
    {
        Console.WriteLine($"key: {settingValue}");
    }
    Console.WriteLine("press `c` to exit, or any key to display re-read the value of `key`");
    var key = Console.ReadKey();
    Console.WriteLine();
    if (key.KeyChar == 'c')
    {
        continueLoop = false;
    }
}
```

> **Note**: With the above example, you can open the `config.xml` file and update the value of `key` and save the file. The application will automatically reload the updated value and prints it to the console.