// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.DependencyInjection.Fallback;
using Xunit;

namespace Microsoft.Data.Entity.SqlServer.FunctionalTests
{
    public class CompositeKeyEndToEndTest
    {
        [Fact]
        public async Task Can_use_two_non_generated_integers_as_composite_key_end_to_end()
        {
            var serviceProvider = new ServiceCollection()
                .AddEntityFramework()
                .AddSqlServer()
                .ServiceCollection
                .BuildServiceProvider();

            var ticks = DateTime.UtcNow.Ticks;

            using (var context = new BronieContext(serviceProvider, "CompositePegasuses"))
            {
                context.Database.EnsureCreated();

                await context.AddAsync(new Pegasus { Id1 = ticks, Id2 = ticks + 1, Name = "Rainbow Dash" });
                await context.SaveChangesAsync();
            }

            using (var context = new BronieContext(serviceProvider, "CompositePegasuses"))
            {
                var pegasus = context.Pegasuses.Single(e => e.Id1 == ticks && e.Id2 == ticks + 1);

                pegasus.Name = "Rainbow Crash";

                await context.SaveChangesAsync();
            }

            using (var context = new BronieContext(serviceProvider, "CompositePegasuses"))
            {
                var pegasus = context.Pegasuses.Single(e => e.Id1 == ticks && e.Id2 == ticks + 1);

                Assert.Equal("Rainbow Crash", pegasus.Name);

                context.Pegasuses.Remove(pegasus);

                await context.SaveChangesAsync();
            }

            using (var context = new BronieContext(serviceProvider, "CompositePegasuses"))
            {
                Assert.Equal(0, context.Pegasuses.Count(e => e.Id1 == ticks && e.Id2 == ticks + 1));
            }
        }

        [Fact]
        public async Task Can_use_generated_values_in_composite_key_end_to_end()
        {
            var serviceProvider = new ServiceCollection()
                .AddEntityFramework()
                .AddSqlServer()
                .ServiceCollection
                .BuildServiceProvider();

            long id1;
            var id2 = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
            Guid id3;

            using (var context = new BronieContext(serviceProvider, "CompositeUnicorns"))
            {
                context.Database.EnsureCreated();

                var added = await context.AddAsync(new Unicorn { Id2 = id2, Name = "Rarity" });

                Assert.True(added.Id1 < 0);
                Assert.NotEqual(Guid.Empty, added.Id3);

                await context.SaveChangesAsync();

                Assert.True(added.Id1 > 0);

                id1 = added.Id1;
                id3 = added.Id3;
            }

            using (var context = new BronieContext(serviceProvider, "CompositeUnicorns"))
            {
                Assert.Equal(1, context.Unicorns.Count(e => e.Id1 == id1 && e.Id2 == id2 && e.Id3 == id3));
            }

            using (var context = new BronieContext(serviceProvider, "CompositeUnicorns"))
            {
                var unicorn = context.Unicorns.Single(e => e.Id1 == id1 && e.Id2 == id2 && e.Id3 == id3);

                unicorn.Name = "Bad Hair Day";

                await context.SaveChangesAsync();
            }

            using (var context = new BronieContext(serviceProvider, "CompositeUnicorns"))
            {
                var unicorn = context.Unicorns.Single(e => e.Id1 == id1 && e.Id2 == id2 && e.Id3 == id3);

                Assert.Equal("Bad Hair Day", unicorn.Name);

                context.Unicorns.Remove(unicorn);

                await context.SaveChangesAsync();
            }

            using (var context = new BronieContext(serviceProvider, "CompositeUnicorns"))
            {
                Assert.Equal(0, context.Unicorns.Count(e => e.Id1 == id1 && e.Id2 == id2 && e.Id3 == id3));
            }
        }

        [Fact]
        public async Task Only_one_part_of_a_composite_key_needs_to_vary_for_uniquness()
        {
            var serviceProvider = new ServiceCollection()
                .AddEntityFramework()
                .AddSqlServer()
                .ServiceCollection
                .BuildServiceProvider();

            var ids = new int[3];

            using (var context = new BronieContext(serviceProvider, "CompositeEarthPonies"))
            {
                context.Database.EnsureCreated();

                var pony1 = await context.AddAsync(new EarthPony { Id2 = 7, Name = "Apple Jack 1" });
                var pony2 = await context.AddAsync(new EarthPony { Id2 = 7, Name = "Apple Jack 2" });
                var pony3 = await context.AddAsync(new EarthPony { Id2 = 7, Name = "Apple Jack 3" });

                await context.SaveChangesAsync();

                ids[0] = pony1.Id1;
                ids[1] = pony2.Id1;
                ids[2] = pony3.Id1;
            }

            using (var context = new BronieContext(serviceProvider, "CompositeEarthPonies"))
            {
                var ponies = context.EarthPonies.ToList();
                Assert.Equal(ponies.Count, ponies.Count(e => e.Name == "Apple Jack 1") * 3);

                Assert.Equal("Apple Jack 1", ponies.Single(e => e.Id1 == ids[0]).Name);
                Assert.Equal("Apple Jack 2", ponies.Single(e => e.Id1 == ids[1]).Name);
                Assert.Equal("Apple Jack 3", ponies.Single(e => e.Id1 == ids[2]).Name);

                ponies.Single(e => e.Id1 == ids[1]).Name = "Pinky Pie 2";

                await context.SaveChangesAsync();
            }

            using (var context = new BronieContext(serviceProvider, "CompositeEarthPonies"))
            {
                var ponies = context.EarthPonies.ToList();
                Assert.Equal(ponies.Count, ponies.Count(e => e.Name == "Apple Jack 1") * 3);

                Assert.Equal("Apple Jack 1", ponies.Single(e => e.Id1 == ids[0]).Name);
                Assert.Equal("Pinky Pie 2", ponies.Single(e => e.Id1 == ids[1]).Name);
                Assert.Equal("Apple Jack 3", ponies.Single(e => e.Id1 == ids[2]).Name);

                context.EarthPonies.RemoveRange(ponies);

                await context.SaveChangesAsync();
            }

            using (var context = new BronieContext(serviceProvider, "CompositeEarthPonies"))
            {
                Assert.Equal(0, context.EarthPonies.Count());
            }
        }

        private class BronieContext : DbContext
        {
            private readonly string _databaseName;

            public BronieContext(IServiceProvider serviceProvider, string databaseName)
                : base(serviceProvider)
            {
                _databaseName = databaseName;
            }

            public DbSet<Pegasus> Pegasuses { get; set; }
            public DbSet<Unicorn> Unicorns { get; set; }
            public DbSet<EarthPony> EarthPonies { get; set; }

            protected override void OnConfiguring(DbContextOptions builder)
            {
                builder.UseSqlServer(SqlServerTestDatabase.CreateConnectionString(_databaseName));
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Pegasus>().Key(e => new { e.Id1, e.Id2 });
                modelBuilder.Entity<Unicorn>().Key(e => new { e.Id1, e.Id2, e.Id3 });
                modelBuilder.Entity<EarthPony>().Key(e => new { e.Id1, e.Id2 });

                var unicornType = modelBuilder.Model.GetEntityType(typeof(Unicorn));

                var id1 = unicornType.GetProperty("Id1");
                id1.ValueGenerationOnAdd = ValueGenerationOnAdd.Client;
                id1.ValueGenerationOnSave = ValueGenerationOnSave.WhenInserting;

                var id3 = unicornType.GetProperty("Id3");
                id3.ValueGenerationOnAdd = ValueGenerationOnAdd.Client;

                var earthType = modelBuilder.Model.GetEntityType(typeof(EarthPony));

                var id = earthType.GetProperty("Id1");
                id.ValueGenerationOnAdd = ValueGenerationOnAdd.Client;
                id.ValueGenerationOnSave = ValueGenerationOnSave.WhenInserting;
            }
        }

        private class Pegasus
        {
            public long Id1 { get; set; }
            public long Id2 { get; set; }
            public string Name { get; set; }
        }

        private class Unicorn
        {
            public int Id1 { get; set; }
            public string Id2 { get; set; }
            public Guid Id3 { get; set; }
            public string Name { get; set; }
        }

        private class EarthPony
        {
            public int Id1 { get; set; }
            public int Id2 { get; set; }
            public string Name { get; set; }
        }
    }
}
