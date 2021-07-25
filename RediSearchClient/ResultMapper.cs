using System.Reflection;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using StackExchange.Redis;
using System.Linq.Expressions;

namespace RediSearchClient
{
    /// <summary>
    /// 
    /// </summary>
    [Obsolete("Use the generic ResultMapper<TTarget> instead.")]
    public static class ResultMapper
    {
        /// <summary>
        /// [Obsolete] Use `CreateMap` from `ResultMapper&lt;TTarget&gt;` instead.
        /// 
        /// This method is for providing the type mappings ahead of time.
        /// </summary>
        /// <param name="mappers"></param>
        /// <typeparam name="TTarget"></typeparam>
        [Obsolete("Use `CreateMap` from `ResultMapper<TTarget>` instead.")]
        public static void CreateMapFor<TTarget>(params ResultMapper<TTarget>.MapperDefinition[] mappers) where TTarget : new() =>
            ResultMapper<TTarget>.CreateMap(mappers);
    }

    /// <summary>
    /// Utility class for mapping a search result to a custom type.
    /// </summary>
    public static class ResultMapper<TTarget> where TTarget : new()
    {
        private static readonly Type TargetType = typeof(TTarget);
        private static readonly PropertyInfo[] TargetTypeProperties = TargetType.GetProperties();

        /// <summary>
        /// Defines a mapping.
        /// </summary>
        public sealed class MapperDefinition
        {
            /// <summary>
            /// The name of the field in the search result.
            /// </summary>
            /// <value></value>
            public string SourceField { get; }

            /// <summary>
            /// The property in the custom type to map the value to.
            /// </summary>
            /// <value></value>
            public PropertyInfo DestinationProperty { get; }

            /// <summary>
            /// Converter function used to convert the RedisResult to whatever type is needed.
            /// </summary>
            /// <value></value>
            public Func<RedisResult, object> Converter { get; }

            /// <summary>
            /// Initializes a MapperDefinition.
            /// </summary>
            /// <param name="sourceField"></param>
            /// <param name="destinationPropertyName"></param>
            /// <param name="converter"></param>
            public MapperDefinition(string sourceField, string destinationPropertyName, Func<RedisResult, object> converter)
            {
                SourceField = sourceField;
                Converter = converter;

                var props = typeof(TTarget).GetProperties();
                var prop = props.FirstOrDefault(x => x.Name == destinationPropertyName);

                if (prop == default)
                {
                    throw new Exception($"Couldn't find a property called: {destinationPropertyName}");
                }
                else
                {
                    DestinationProperty = prop;
                }
            }

            /// <summary>
            /// Initializes a MapperDefinition.
            /// </summary>
            /// <param name="sourceField"></param>
            /// <param name="destinationProperty"></param>
            /// <param name="converter"></param>
            public MapperDefinition(string sourceField, PropertyInfo destinationProperty, Func<RedisResult, object> converter)
            {
                SourceField = sourceField;
                DestinationProperty = destinationProperty;
                Converter = converter;
            }

            /// <summary>
            /// Allows for the implict conversion between a (string, string, Func&lt;RedisResult, object&gt;) tuple to a "MapperDefinition".
            /// </summary>
            /// <param name="source"></param>
            public static implicit operator MapperDefinition((string sourceField, string destinationPropertyName, Func<RedisResult, object> converter) source)
            {
                return new MapperDefinition(source.sourceField, source.destinationPropertyName, source.converter);
            }
        }

        internal class MapperDefinitionContainer
        {
            public List<MapperDefinition> Mappers { get; }

            public MapperDefinitionContainer(MapperDefinition[] mappers)
            {
                Mappers = new List<MapperDefinition>(mappers);
            }

            public TTarget Apply(SearchResultItem searchResultItem)
            {
                var result = new TTarget();

                if (_mapperDefinitions.TryGetValue(typeof(TTarget), out var mapper))
                {
                    foreach (var m in mapper.Mappers)
                    {
                        m.DestinationProperty.SetValue(result, m.Converter(searchResultItem[m.SourceField]));
                    }
                }
                else
                {
                    // TODO: Throw an exception here if the mapper isn't found. It should have been created by now though...
                }

                return result;
            }

            public TTarget Apply(AggregateResultCollection aggregateCollection)
            {
                var result = new TTarget();

                if (_mapperDefinitions.TryGetValue(typeof(TTarget), out var mapper))
                {
                    foreach (var m in mapper.Mappers)
                    {
                        m.DestinationProperty.SetValue(result, m.Converter(aggregateCollection[m.SourceField]));
                    }
                }
                else
                {
                    // TODO: Throw an exception here if the mapper isn't found. It should have been created by now though...
                }

                return result;
            }
        }

        private static ConcurrentDictionary<Type, MapperDefinitionContainer> _mapperDefinitions =
            new ConcurrentDictionary<Type, MapperDefinitionContainer>();

        /// <summary>
        /// This method is for providing the type mappings ahead of time.
        /// </summary>
        /// <param name="mappers"></param>
        public static void CreateMap(params MapperDefinition[] mappers)
        {
            if (!_mapperDefinitions.ContainsKey(typeof(TTarget)))
            {
                _mapperDefinitions.TryAdd(typeof(TTarget), new MapperDefinitionContainer(mappers));
            }
        }

        internal static void AppendMap(string sourceField, PropertyInfo destinationProperty, Func<RedisResult, object> converter)
        {
            var mapperDefinition = new MapperDefinition(sourceField, destinationProperty, converter);

            if (_mapperDefinitions.TryGetValue(typeof(TTarget), out var mapperDefinitionContainer))
            {
                mapperDefinitionContainer.Mappers.Add(mapperDefinition);
            }
            else
            {
                CreateMap(mapperDefinition);
            }
        }

        private static object ConvertRedisResultToString(RedisResult result) => (string)result;

        private static object ConvertRedisResultToInteger(RedisResult result) => (int)result;

        private static object ConvertRedisResultToDouble(RedisResult result) => (double)result;

        public class MapperBuilder
        {
            internal MapperBuilder() { }

            public MapperBuilder ForStringMember(Expression<Func<TTarget, object>> destinationProperty) =>
                ForMember(destinationProperty, ConvertRedisResultToString);

            public MapperBuilder ForStringMember(string sourceField, Expression<Func<TTarget, object>> destinationProperty) =>
                ForMember(sourceField, destinationProperty, ConvertRedisResultToString);

            public MapperBuilder ForIntegerMember(Expression<Func<TTarget, object>> destinationProperty) =>
                ForMember(destinationProperty, ConvertRedisResultToInteger);

            public MapperBuilder ForIntegerMember(string sourceField, Expression<Func<TTarget, object>> destinationProperty) =>
                ForMember(sourceField, destinationProperty, ConvertRedisResultToInteger);

            public MapperBuilder ForDoubleMember(Expression<Func<TTarget, object>> destinationProperty) =>
                ForMember(destinationProperty, ConvertRedisResultToDouble);

            public MapperBuilder ForDoubleMember(string sourceField, Expression<Func<TTarget, object>> destinationProperty) =>
                ForMember(sourceField, destinationProperty, ConvertRedisResultToDouble);

            public MapperBuilder ForMember(Expression<Func<TTarget, object>> destinationProperty, Func<RedisResult, object> converter)
            {
                var destinationPropertyInfo = GetByExpression(destinationProperty);
                var sourceField = destinationPropertyInfo.Name;

                AppendMap(sourceField, destinationPropertyInfo, converter);

                return this;
            }

            public MapperBuilder ForMember(string sourceField, Expression<Func<TTarget, object>> destinationProperty, Func<RedisResult, object> converter)
            {
                var propertyInfo = GetByExpression(destinationProperty);

                AppendMap(sourceField, propertyInfo, converter);

                return this;
            }

            private static PropertyInfo GetByExpression<TPropertyType>(Expression<Func<TTarget, TPropertyType>> destinationPropertyExpression)
            {
                if (destinationPropertyExpression == default)
                {
                    throw new ArgumentNullException(nameof(destinationPropertyExpression));
                }

                var property = default(MemberExpression);

                if (destinationPropertyExpression.Body.NodeType == ExpressionType.Convert)
                {
                    var convert = destinationPropertyExpression.Body as UnaryExpression;

                    if (convert != default)
                    {
                        property = convert.Operand as MemberExpression;
                    }
                }

                if (property == default)
                {
                    property = destinationPropertyExpression.Body as MemberExpression;
                }

                if (property == default)
                {
                    throw new ArgumentException("The expression cannot be null and should be passed in the format of: x => x.PropertyName");
                }

                var propertyInfo = property.Member as PropertyInfo;

                return propertyInfo;
            }
        }

        public static MapperBuilder CreateMap()
        {
            return new MapperBuilder();
        }

        /// <summary>
        /// Creates a type mapping for types that... don't have type mappings.
        /// </summary>
        internal static void SynthesizeMapFor()
        {
            var mappers = new List<MapperDefinition>();

            foreach (var p in typeof(TTarget).GetProperties())
            {
                mappers.Add((p.Name, p.Name, CreateConverter(p)));
            }

            CreateMap(mappers.ToArray());
        }

        /// <summary>
        /// Creates a converter delegate. 
        /// 
        /// !!! If you want to add more supported types to the mapper then this is where you would do that. !!!
        /// </summary>
        /// <param name="propertyInfo"></param>
        /// <returns></returns>
        private static Func<RedisResult, object> CreateConverter(PropertyInfo propertyInfo)
        {
            var type = propertyInfo.PropertyType;

            if (type == typeof(int))
            {
                return (r) => (int)r;
            }
            else if (type == typeof(long))
            {
                return (r) => (long)r;
            }
            else if (type == typeof(double))
            {
                return (r) => (double)r;
            }
            else
            {
                return (r) => r.ToString();
            }
        }

        private static void RegisterMapper(MapperDefinition[] mappers)
        {
            if (!_mapperDefinitions.ContainsKey(typeof(TTarget)))
            {
                if (!mappers?.Any() ?? true)
                {
                    SynthesizeMapFor();
                }
                else
                {
                    CreateMap(mappers.ToArray());
                }

            }
        }

        /// <summary>
        /// Maps a SearchResult collection to a collection of... whatever you want.
        /// </summary>
        /// <param name="searchResult">The search result.</param>
        /// <param name="mappers">Optional instructions for mapping the result to a custom type.</param>
        /// <returns></returns>
        public static IEnumerable<TTarget> MapTo(SearchResult searchResult, params MapperDefinition[] mappers)
        {
            RegisterMapper(mappers);

            if (_mapperDefinitions.TryGetValue(typeof(TTarget), out var mapper))
            {
                foreach (var sr in searchResult)
                {
                    yield return mapper.Apply(sr);
                }
            }

            yield break;
        }

        /// <summary>
        /// Maps an AggregateResult collection to a collection of custom types. 
        /// </summary>
        /// <param name="aggregateResult">The aggregate result.</param>
        /// <param name="mappers">Optional instructions for mapping the result to a custom type.</param>
        /// <returns></returns>
        public static IEnumerable<TTarget> MapTo(AggregateResult aggregateResult, params MapperDefinition[] mappers)
        {
            RegisterMapper(mappers);

            if (_mapperDefinitions.TryGetValue(typeof(TTarget), out var mapper))
            {
                foreach (var ar in aggregateResult)
                {
                    yield return mapper.Apply(ar);
                }
            }

            yield break;
        }
    }
}