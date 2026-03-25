// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.IO;
using log4net;
using NSubstitute;
using Schema.Isolators;
using Schema.Utility;
using Tongs = SchemaTongs.SchemaTongs;

namespace SchemaTongs.UnitTests;

public class RenameLegacyTableDataFolderTests
{
    [Test]
    public void ShouldCopyFilesAndDeleteLegacy_WhenLegacyExistsAndNewDoesNot()
    {
        var progressLog = Substitute.For<ILog>();
        var directory = Substitute.For<IDirectory>();
        var file = Substitute.For<IFile>();

        var templatePath = @"C:\repo\templates\MyTemplate";
        var legacyPath = Path.Combine(templatePath, "TableData");
        var newPath = Path.Combine(templatePath, "Table Data");

        directory.Exists(legacyPath).Returns(true);
        directory.Exists(newPath).Returns(false);
        directory.GetFiles(legacyPath, "*.*", SearchOption.AllDirectories)
            .Returns(new[] { Path.Combine(legacyPath, "dbo.Users.json") });

        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(directory);
            FactoryContainer.Register(file);

            var tongs = new Tongs();
            tongs._templatePath = templatePath;

            tongs.RenameLegacyTableDataFolder();

            directory.Received(2).CreateDirectory(newPath);
            file.Received(1).Copy(
                Path.Combine(legacyPath, "dbo.Users.json"),
                Path.Combine(newPath, "dbo.Users.json"));
            directory.Received(1).Delete(legacyPath, true);
            progressLog.Received(1).Info("Renamed legacy 'TableData' folder to 'Table Data' for consistency.");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldLogWarning_WhenBothFoldersExist()
    {
        var progressLog = Substitute.For<ILog>();
        var directory = Substitute.For<IDirectory>();
        var file = Substitute.For<IFile>();

        var templatePath = @"C:\repo\templates\MyTemplate";
        var legacyPath = Path.Combine(templatePath, "TableData");
        var newPath = Path.Combine(templatePath, "Table Data");

        directory.Exists(legacyPath).Returns(true);
        directory.Exists(newPath).Returns(true);

        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(directory);
            FactoryContainer.Register(file);

            var tongs = new Tongs();
            tongs._templatePath = templatePath;

            tongs.RenameLegacyTableDataFolder();

            directory.DidNotReceive().CreateDirectory(Arg.Any<string>());
            file.DidNotReceive().Copy(Arg.Any<string>(), Arg.Any<string>());
            directory.DidNotReceive().Delete(Arg.Any<string>(), Arg.Any<bool>());
            progressLog.Received(1).Warn("Both 'TableData' and 'Table Data' folders exist. Please resolve manually.");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldDoNothing_WhenNeitherFolderExists()
    {
        var progressLog = Substitute.For<ILog>();
        var directory = Substitute.For<IDirectory>();
        var file = Substitute.For<IFile>();

        var templatePath = @"C:\repo\templates\MyTemplate";
        var legacyPath = Path.Combine(templatePath, "TableData");
        var newPath = Path.Combine(templatePath, "Table Data");

        directory.Exists(legacyPath).Returns(false);
        directory.Exists(newPath).Returns(false);

        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(directory);
            FactoryContainer.Register(file);

            var tongs = new Tongs();
            tongs._templatePath = templatePath;

            tongs.RenameLegacyTableDataFolder();

            directory.DidNotReceive().CreateDirectory(Arg.Any<string>());
            file.DidNotReceive().Copy(Arg.Any<string>(), Arg.Any<string>());
            directory.DidNotReceive().Delete(Arg.Any<string>(), Arg.Any<bool>());
            progressLog.DidNotReceive().Info(Arg.Any<string>());
            progressLog.DidNotReceive().Warn(Arg.Any<string>());

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }

    [Test]
    public void ShouldCreateSubdirectories_WhenLegacyHasNestedFiles()
    {
        var progressLog = Substitute.For<ILog>();
        var directory = Substitute.For<IDirectory>();
        var file = Substitute.For<IFile>();

        var templatePath = @"C:\repo\templates\MyTemplate";
        var legacyPath = Path.Combine(templatePath, "TableData");
        var newPath = Path.Combine(templatePath, "Table Data");

        directory.Exists(legacyPath).Returns(true);
        directory.Exists(newPath).Returns(false);
        directory.GetFiles(legacyPath, "*.*", SearchOption.AllDirectories)
            .Returns(new[]
            {
                Path.Combine(legacyPath, "subdir", "dbo.Orders.json"),
                Path.Combine(legacyPath, "subdir", "nested", "dbo.Items.json")
            });

        lock (FactoryContainer.SharedLockObject)
        {
            LogFactory.Register("ProgressLog", progressLog);
            FactoryContainer.Register(directory);
            FactoryContainer.Register(file);

            var tongs = new Tongs();
            tongs._templatePath = templatePath;

            tongs.RenameLegacyTableDataFolder();

            directory.Received(1).CreateDirectory(newPath);
            directory.Received(1).CreateDirectory(Path.Combine(newPath, "subdir"));
            directory.Received(1).CreateDirectory(Path.Combine(newPath, "subdir", "nested"));
            file.Received(1).Copy(
                Path.Combine(legacyPath, "subdir", "dbo.Orders.json"),
                Path.Combine(newPath, "subdir", "dbo.Orders.json"));
            file.Received(1).Copy(
                Path.Combine(legacyPath, "subdir", "nested", "dbo.Items.json"),
                Path.Combine(newPath, "subdir", "nested", "dbo.Items.json"));
            directory.Received(1).Delete(legacyPath, true);
            progressLog.Received(1).Info("Renamed legacy 'TableData' folder to 'Table Data' for consistency.");

            FactoryContainer.Clear();
            LogFactory.Clear();
        }
    }
}
