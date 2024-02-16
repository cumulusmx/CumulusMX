using System.Collections.Generic;
using System.Text;

namespace CumulusMX
{
	internal class MySqlTable(string TableName)
	{
		internal string Name = TableName;
		internal List<Column> Columns = [];

		private string _insertStart;

		public string CreateCommand
		{
			get
			{
				var create = new StringBuilder($"CREATE TABLE {Name} (", 2048);
				// add the columns
				foreach (var col in Columns)
				{
					create.Append($"{col.Name} {col.Attributes},");
				}

				// add the primary key (if any)
				if (!string.IsNullOrEmpty(PrimaryKey))
				{
					create.Append($" PRIMARY KEY ({PrimaryKey})");
				}

				// close the column list
				create.Append(')');

				// strip trailing comma (if any)
				if (create[^1] == ',')
				{
					create.Length--;
				}

				// add the comment (if any)
				if (!string.IsNullOrEmpty(Comment))
				{
					create.Append(" COMMENT=" + Comment);
				}

				return create.ToString();
			}
		}


		public string StartOfInsert
		{
			get
			{
				if (_insertStart == null)
				{
					var insert = new StringBuilder($"INSERT IGNORE INTO {Name} (", 2048);
					foreach (var col in Columns)
					{
						insert.Append(col.Name + ",");
					}

					// replace trailing comma with closing brace
					insert[^1] = ')';
					_insertStart = insert.ToString();
				}

				return _insertStart;
			}
		}

		public string PrimaryKey { get; set; }

		public string Comment { get; set; }


		public void AddColumn(string ColName, string ColAttributes)
		{
			Columns.Add(new Column(ColName, ColAttributes));
		}

		public void Rebuild()
		{
			// reset the strings so they are recreated on next use
			_insertStart = null;
		}

		internal class Column(string ColName, string ColAttributes)
		{
			internal string Name = ColName;
			internal string Attributes = ColAttributes;
		}
	}
}
