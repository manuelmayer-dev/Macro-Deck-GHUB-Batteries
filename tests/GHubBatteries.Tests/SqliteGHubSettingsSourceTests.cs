using System.Text;
using GHubBatteries.GHub;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace GHubBatteries.Tests;

[TestFixture]
public sealed class SqliteGHubSettingsSourceTests
{
	private string _directory = null!;
	private string _databasePath = null!;

	[SetUp]
	public void SetUp()
	{
		_directory = Path.Combine(Path.GetTempPath(), "ghub-batteries-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_directory);
		_databasePath = Path.Combine(_directory, "settings.db");
	}

	[TearDown]
	public void TearDown() => Directory.Delete(_directory, recursive: true);

	private static SqliteGHubSettingsSource Source(string? path) => new(new FixedLocation(path));

	private void CreateDatabase(params string[] documents)
	{
		using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _databasePath, Pooling = false }.ToString());
		connection.Open();
		using var create = connection.CreateCommand();
		create.CommandText = """
			PRAGMA journal_mode=WAL;
			CREATE TABLE data(_id INTEGER PRIMARY KEY, _date_created datetime default current_timestamp, file BLOB NOT NULL);
			""";
		create.ExecuteNonQuery();

		foreach (var document in documents)
		{
			using var insert = connection.CreateCommand();
			insert.CommandText = "INSERT INTO data(file) VALUES ($file)";
			insert.Parameters.AddWithValue("$file", Encoding.UTF8.GetBytes(document));
			insert.ExecuteNonQuery();
		}
	}

	[Test]
	public async Task Reads_the_newest_settings_document()
	{
		CreateDatabase("""{"old":1}""", """{"new":2}""");

		var snapshot = await Source(_databasePath).ReadAsync(CancellationToken.None);

		Assert.That(snapshot.Availability, Is.EqualTo(GHubAvailability.Available));
		Assert.That(Encoding.UTF8.GetString(snapshot.Json), Is.EqualTo("""{"new":2}"""));
	}

	[Test]
	public async Task An_empty_table_is_an_empty_document()
	{
		CreateDatabase();

		var snapshot = await Source(_databasePath).ReadAsync(CancellationToken.None);

		Assert.That(GHubBatteryParser.Parse(snapshot.Json), Is.Empty);
	}

	[Test]
	public async Task A_missing_database_is_not_found()
	{
		var snapshot = await Source(_databasePath).ReadAsync(CancellationToken.None);

		Assert.That(snapshot.Availability, Is.EqualTo(GHubAvailability.NotFound));
	}

	[Test]
	public async Task No_path_means_the_platform_is_unsupported()
	{
		var snapshot = await Source(null).ReadAsync(CancellationToken.None);

		Assert.That(snapshot.Availability, Is.EqualTo(GHubAvailability.UnsupportedPlatform));
	}

	[Test]
	public async Task Reading_releases_the_database_file()
	{
		CreateDatabase("{}");

		await Source(_databasePath).ReadAsync(CancellationToken.None);

		Assert.That(() => File.Move(_databasePath, _databasePath + ".moved"), Throws.Nothing);
	}

	private sealed class FixedLocation(string? path) : IGHubSettingsLocation
	{
		public string? GetSettingsDatabasePath() => path;
	}
}
