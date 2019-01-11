using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace CumulusMX.Data
{
    public class WeatherDataContext : DbContext
    {
        private readonly string _dataFile;


        public DbSet<WeatherData> WeatherData { get; private set; }


        public WeatherDataContext(string dataFile)
        {
            this._dataFile = dataFile;
        } 

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={_dataFile}");
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
        }
    }
}
