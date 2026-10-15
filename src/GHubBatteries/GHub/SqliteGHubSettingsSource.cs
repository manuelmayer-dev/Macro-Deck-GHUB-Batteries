using Microsoft.Data.Sqlite;

namespace GHubBatteries.GHub;

internal sealed class SqliteGHubSettingsSource(IGHubSettingsLocation location) : IGHubSettingsSource
{
	private static readonly byte[] _emptyDocument = "{}"u8.ToArray();

	public async Task<GHubSettingsSnapshot> ReadAsync(CancellationToken cancellationToken)
	{
		var path = location.GetSettingsDatabasePath();
		if (path is null)
		{
			return GHubSettingsSnapshot.UnsupportedPlatform;
		}

		if (!File.Exists(path))
		{
			return GHubSettingsSnapshot.NotFound;
		}

		// Read-only and unpooled so no handle is kept between polls; the short timeout keeps a database
		// G HUB is busy writing from stalling shutdown.
		var connectionString = new SqliteConnectionStringBuilder
		{
			DataSource = path,
			Mode = SqliteOpenMode.ReadOnly,
			Pooling = false,
			DefaultTimeout = 2
		}.ToString();

		await using var connection = new SqliteConnection(connectionString);
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

		await using var command = connection.CreateCommand();
		command.CommandText = "SELECT file FROM data ORDER BY _id DESC LIMIT 1";

		await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
		if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false) || reader.IsDBNull(0))
		{
			return GHubSettingsSnapshot.Available(_emptyDocument);
		}

		return GHubSettingsSnapshot.Available(reader.GetFieldValue<byte[]>(0));
	}
}
