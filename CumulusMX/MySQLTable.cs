using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CumulusMX
{
	internal class MySqlTable
	{
		internal string Name;
		internal List<Column> Columns;

		private string _insertStart;
		private string _PrimaryKey;
		private string _Comment;

		public MySqlTable(string TableName)
		{
			Name = TableName;
			Columns = new List<Column>();
		}


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
				if (!string.IsNullOrEmpty(_PrimaryKey))
				{
					create.Append($" PRIMARY KEY ({_PrimaryKey})");
				}

				// close the column list
				create.Append(')');

				// strip trailing comma (if any)
				if (create[create.Length - 1] == ',')
				{
					create.Length--;
				}

				// add the comment (if any)
				if (!string.IsNullOrEmpty(_Comment))
				{
					create.Append(" COMMENT=" + _Comment);
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
					insert[insert.Length - 1] = ')';
					_insertStart = insert.ToString();
				}

				return _insertStart;
			}
		}

		public string PrimaryKey
		{
			get 
			{	
				return _PrimaryKey;
			}
			set
			{
				_PrimaryKey = value;
			}
		}

		public string Comment
		{
			get
			{
				return _Comment;
			}
			set
			{
				_Comment = value;
			}
		}


		public void AddColumn(string ColName, string ColAttributes)
		{
			Columns.Add(new Column(ColName, ColAttributes));
		}

		public void Rebuild()
		{
			// reset the strings so they are recreated on next use
			_insertStart = null;
		}

		internal class Column
		{
			internal string Name;
			internal string Attributes;

			public Column (string ColName, string ColAttributes)
			{
				Name = ColName;
				Attributes = ColAttributes;
			}
		}
	}
}
