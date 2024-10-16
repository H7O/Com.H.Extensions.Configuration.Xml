using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using System.Xml.Linq;

namespace Com.H.Extensions.Configuration.Xml;

/// <summary>
/// A configuration provider that supports reading and writing configuration settings from an XML file.
/// </summary>
public class XmlWritableConfigurationProvider : FileConfigurationProvider, IDisposable
{
    private readonly ReaderWriterLockSlim _dataLock = new ReaderWriterLockSlim();
    private static readonly object _fileLock = new object();
    private IDisposable _changeTokenRegistration = null!;
    private CancellationTokenSource _reloadTokenSource = new CancellationTokenSource();
    private bool _disposed = false; // To detect redundant calls

    private string _rootName = "configuration"; // Default root name

    private readonly Dictionary<string, bool> _cdataKeys = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The name of the root element of the XML document.
    /// </summary>
    public string RootName
    {
        get => _rootName;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("RootName cannot be null or empty.");
            }
            _rootName = value;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlWritableConfigurationProvider"/> class.
    /// </summary>
    /// <param name="source">The source settings for this provider.</param>
    public XmlWritableConfigurationProvider(FileConfigurationSource source) : base(source)
    {
        if (source.ReloadOnChange && !string.IsNullOrWhiteSpace(source.Path))
        {
            _changeTokenRegistration = ChangeToken.OnChange(
                () => source?.FileProvider?.Watch(source.Path),
                () => OnFileChanged());
        }
    }

    /// <summary>
    /// Loads the configuration data from the file.
    /// </summary>
    public override void Load()
    {
        lock (_fileLock)
        {
            _dataLock.EnterWriteLock();
            try
            {
                if (File.Exists(Source.Path))
                {
                    using (var stream = new FileStream(Source.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        Load(stream);
                    }
                }
                else if (Source.Optional)
                {
                    // Use default root name since file doesn't exist
                    Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                    _cdataKeys.Clear();
                }
                else
                {
                    throw new FileNotFoundException($"The configuration file '{Source.Path}' was not found and is not optional.");
                }
            }
            finally
            {
                _dataLock.ExitWriteLock();
            }
        }
    }

    /// <summary>
    /// Loads the configuration data from the specified stream.
    /// </summary>
    /// <param name="stream">The stream to read the configuration data from.</param>
    public override void Load(Stream stream)
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var cdataKeys = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        var doc = XDocument.Load(stream);
        var root = doc.Root;

        if (root == null)
        {
            throw new FormatException("XML configuration file has no root element.");
        }

        // Store the root element name
        _rootName = root.Name.LocalName;

        foreach (var child in root.Elements())
        {
            LoadElement(child, data, cdataKeys, parentPath: null!);
        }

        // Replace the Data dictionary atomically
        Data = data;
        _cdataKeys.Clear();
        foreach (var kvp in cdataKeys)
        {
            _cdataKeys[kvp.Key] = kvp.Value;
        }
    }

    /// <summary>
    /// Loads an XML element and its children into the configuration data.
    /// </summary>
    /// <param name="element">The XML element to load.</param>
    /// <param name="data">The dictionary to store the configuration data.</param>
    /// <param name="cdataKeys">The dictionary to store CDATA flags.</param>
    /// <param name="parentPath">The parent path of the current element.</param>
    private void LoadElement(XElement element, Dictionary<string, string?> data, Dictionary<string, bool> cdataKeys, string parentPath)
    {
        var path = parentPath == null
            ? element.Name.LocalName
            : ConfigurationPath.Combine(parentPath, element.Name.LocalName);

        if (!element.HasElements && !element.Nodes().Any(n => n is XElement))
        {
            var value = element.Value;
            data[path] = value;

            // Check if the element contains a CDATA section
            var hasCData = element.Nodes().OfType<XCData>().Any();
            cdataKeys[path] = hasCData;
        }
        else
        {
            foreach (var child in element.Elements())
            {
                LoadElement(child, data, cdataKeys, path);
            }
        }
    }

    /// <summary>
    /// Tries to get a configuration value for the specified key.
    /// </summary>
    /// <param name="key">The key of the configuration value.</param>
    /// <param name="value">The value of the configuration setting.</param>
    /// <returns>True if the key was found, otherwise false.</returns>
    public override bool TryGet(string key, out string? value)
    {
        _dataLock.EnterReadLock();
        try
        {
            return Data.TryGetValue(key, out value);
        }
        finally
        {
            _dataLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Sets a configuration value for the specified key.
    /// </summary>
    /// <param name="key">The key of the configuration value.</param>
    /// <param name="value">The value of the configuration setting.</param>
    public override void Set(string key, string? value)
    {
        _dataLock.EnterWriteLock();
        try
        {
            Data[key] = value;

            // Preserve CDATA flag for existing keys
            if (!_cdataKeys.ContainsKey(key))
            {
                _cdataKeys[key] = false;
            }
        }
        finally
        {
            _dataLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Sets a configuration value for the specified key with CDATA section.
    /// </summary>
    /// <param name="key">The key of the configuration value.</param>
    /// <param name="value">The value of the configuration setting.</param>
    public void SetWithCData(string key, string value)
    {
        _dataLock.EnterWriteLock();
        try
        {
            Data[key] = value;
            _cdataKeys[key] = true;
        }
        finally
        {
            _dataLock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Saves the configuration data to the file.
    /// </summary>
    public void Save()
    {
        lock (_fileLock)
        {
            Dictionary<string, string?> dataCopy;
            Dictionary<string, bool> cdataKeysCopy;

            // Create copies of the Data and _cdataKeys dictionaries under a read lock
            _dataLock.EnterReadLock();
            try
            {
                dataCopy = new Dictionary<string, string?>(Data);
                cdataKeysCopy = new Dictionary<string, bool>(_cdataKeys);
            }
            finally
            {
                _dataLock.ExitReadLock();
            }

            var doc = new XDocument();
            var root = new XElement(_rootName);
            doc.Add(root);

            foreach (var kvp in dataCopy)
            {
                var keys = kvp.Key.Split(ConfigurationPath.KeyDelimiter);

                XElement current = root;

                foreach (var key in keys)
                {
                    var child = current.Element(key);
                    if (child == null)
                    {
                        child = new XElement(key);
                        current.Add(child);
                    }
                    current = child;
                }

                // Determine if the value should be written as CDATA
                bool useCData = cdataKeysCopy.TryGetValue(kvp.Key, out bool isCData) && isCData;

                if (useCData)
                {
                    current.Add(new XCData(kvp.Value??string.Empty));
                }
                else
                {
                    current.Value = kvp.Value ?? string.Empty;
                }
            }

            // Write the XML document to the file
            if (string.IsNullOrWhiteSpace(Source.Path))
            {
                throw new InvalidOperationException("The configuration source path is not set.");
            }
            var directory = Path.GetDirectoryName(Source.Path);
            if (!Directory.Exists(directory)
                && !string.IsNullOrWhiteSpace(directory)
                )
            {
                Directory.CreateDirectory(directory);
            }
            using (var stream = new FileStream(Source.Path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                doc.Save(stream);
            }
        }
    }

    /// <summary>
    /// Handles the file change event.
    /// </summary>
    private void OnFileChanged()
    {
        DebounceReload();
    }

    /// <summary>
    /// Debounces the reload operation to avoid frequent reloads.
    /// </summary>
    private void DebounceReload()
    {
        _reloadTokenSource.Cancel();
        _reloadTokenSource = new CancellationTokenSource();

        Task.Delay(TimeSpan.FromMilliseconds(500), _reloadTokenSource.Token)
            .ContinueWith(task =>
            {
                if (!task.IsCanceled)
                {
                    Load();
                    OnReload();
                }
            }, TaskScheduler.Default);
    }

    /// <summary>
    /// Releases the resources used by the <see cref="XmlWritableConfigurationProvider"/> class.
    /// </summary>
    public new void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="XmlWritableConfigurationProvider"/> class and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _changeTokenRegistration?.Dispose();
            _dataLock?.Dispose();
            _reloadTokenSource?.Dispose();
        }

        base.Dispose();

        _disposed = true;
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="XmlWritableConfigurationProvider"/> class.
    /// </summary>
    ~XmlWritableConfigurationProvider()
    {
        Dispose(false);
    }
}
