using System;
using System.IO.Compression;
using System.IO;
using System.Linq;

namespace PinShot.Tests;

[TestClass]
public sealed class NoteStoreTests
{
    private string _root = "";

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "PinShot.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        NoteStore.RootDirectoryOverride = _root;
    }

    [TestCleanup]
    public void Cleanup()
    {
        NoteStore.RootDirectoryOverride = null;
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [TestMethod]
    public void SaveAndLoadOrdersPinnedNotesFirst()
    {
        var regular = new NoteItem { Text = "Regular", Modified = DateTime.Now.AddDays(1) };
        var pinned = new NoteItem { Text = "Pinned", IsPinned = true, Modified = DateTime.Now.AddDays(-1) };

        Assert.IsTrue(NoteStore.Save(regular, touch: false));
        Assert.IsTrue(NoteStore.Save(pinned, touch: false));

        var loaded = NoteStore.LoadAll();
        Assert.AreEqual("Pinned", loaded[0].Text);
        Assert.AreEqual("Regular", loaded[1].Text);
    }

    [TestMethod]
    public void LoadRecoversPreviousVersionWhenNoteJsonIsCorrupt()
    {
        var note = new NoteItem { Text = "Versión recuperable" };
        Assert.IsTrue(NoteStore.Save(note));
        note.Text = "Versión más reciente";
        Assert.IsTrue(NoteStore.Save(note));

        File.WriteAllText(Path.Combine(NoteStore.NoteDir(note.Id), "nota.json"), "{ inválido");

        var loaded = NoteStore.LoadAll();
        Assert.HasCount(1, loaded);
        Assert.AreEqual("Versión recuperable", loaded[0].Text);
    }

    [TestMethod]
    public void ExportAndImportPreservesNoteWithoutReplacingExistingId()
    {
        var note = new NoteItem
        {
            Text = "Nota para importar",
            IsPinned = true,
            Tags = new List<string> { "trabajo" }
        };
        Assert.IsTrue(NoteStore.Save(note));

        string archivePath = Path.Combine(_root, "respaldo.zip");
        NoteStore.ExportNotes(archivePath);

        int imported = NoteStore.ImportNotes(archivePath);

        Assert.AreEqual(1, imported);
        var restored = NoteStore.LoadAll();
        Assert.HasCount(2, restored);
        var original = restored.Single(item => item.Id == note.Id);
        var importedNote = restored.Single(item => item.Id != note.Id);
        Assert.AreEqual(note.Text, original.Text);
        Assert.AreEqual(note.Text, importedNote.Text);
        Assert.IsTrue(importedNote.IsPinned);
        CollectionAssert.AreEqual(note.Tags, importedNote.Tags);
    }

    [TestMethod]
    public void ImportRejectsArchivePathTraversal()
    {
        string archivePath = Path.Combine(_root, "malicioso.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(archive.CreateEntry("../escape.txt").Open()))
        {
            writer.Write("no debe extraerse");
        }

        Assert.Throws<InvalidDataException>(() => NoteStore.ImportNotes(archivePath));
        Assert.IsFalse(File.Exists(Path.Combine(_root, "escape.txt")));
    }
}
