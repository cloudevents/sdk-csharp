// Copyright 2023 Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CloudNative.CloudEvents.UnitTests.ConformanceTestData;

internal class TestDataProvider
{
    private static readonly string ConformanceTestDataRoot = Path.Combine(FindRepoRoot(), "conformance", "format");

    public static TestDataProvider Json { get; } = new TestDataProvider("json", "*.json");
    public static TestDataProvider Protobuf { get; } = new TestDataProvider("protobuf", "*.json");
    public static TestDataProvider Xml { get; } = new TestDataProvider("xml", "*.xml");


    private readonly string testDataDirectory;
    private readonly string searchPattern;

    private TestDataProvider(string relativeDirectory, string searchPattern)
    {
        testDataDirectory = Path.Combine(ConformanceTestDataRoot, relativeDirectory);
        this.searchPattern = searchPattern;
    }

    public IEnumerable<string> ListTestFiles() => Directory.EnumerateFiles(testDataDirectory, searchPattern);

    /// <summary>
    /// Loads all tests, assuming multiple tests per file, to be loaded based on textual file content.
    /// </summary>
    /// <typeparam name="TFile">The deserialized test file type.</typeparam>
    /// <typeparam name="TTest">The deserialized test type.</typeparam>
    /// <param name="fileParser">A function to parse the content of the file (provided as a string) to a test file.</param>
    /// <param name="testExtractor">A function to extract all the tests within the given test file.</param>
    public IReadOnlyList<TTest> LoadTests<TFile, TTest>(Func<string, TFile> fileParser, Func<TFile, IEnumerable<TTest>> testExtractor) =>
        ListTestFiles()
        .Select(file => fileParser(File.ReadAllText(file)))
        .SelectMany(testExtractor)
        .ToList()
        .AsReadOnly();

    private static string FindRepoRoot()
    {
        var currentDirectory = Path.GetFullPath(".");
        var directory = new DirectoryInfo(currentDirectory);
        while (directory != null &&
            (!File.Exists(Path.Combine(directory.FullName, "LICENSE"))
            || !File.Exists(Path.Combine(directory.FullName, "CloudEvents.sln"))))
        {
            directory = directory.Parent;
        }
        if (directory == null)
        {
            throw new Exception("Unable to determine root directory. Please run within the sdk-csharp repository.");
        }
        return directory.FullName;
    }
}
