using GraphQL.Types;
using Sitecore;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Linq;
using Sitecore.ContentSearch.Linq.Utilities;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Data.Managers;
using Sitecore.Globalization;
using Sitecore.Services.GraphQL.Content;
using Sitecore.Services.GraphQL.Content.GraphTypes;
using Sitecore.Services.GraphQL.Content.GraphTypes.ContentSearch;
using Sitecore.Services.GraphQL.GraphTypes.Connections;
using Sitecore.Services.GraphQL.Schemas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace AdvanceGraphQLQuery.Queries
{
    public class CustomSearchQuery : RootFieldType<ContentSearchResultsGraphType, ContentSearchResults>, IContentSchemaRootFieldType
    {
        public CustomSearchQuery() : base(name: "customSearchQuery", description: "custom Search query")
        {
            QueryArguments queryArguments1 = new QueryArguments(Array.Empty<QueryArgument>());
            queryArguments1.AddConnectionArguments();

            QueryArguments queryArguments2 = queryArguments1;
            QueryArgument<StringGraphType> queryArgument1 = new QueryArgument<StringGraphType>();
            queryArgument1.Name = "rootItem";
            queryArgument1.Description = "ID or path of an item to search under (results will be descendants)";
            queryArguments2.Add(queryArgument1);

            QueryArguments queryArguments3 = queryArguments1;
            QueryArgument<StringGraphType> queryArgument2 = new QueryArgument<StringGraphType>();
            queryArgument2.Name = "language";
            queryArgument2.Description = "The item language to request (defaults to the context language)";
            queryArguments3.Add(queryArgument2);

            QueryArguments queryArguments4 = queryArguments1;
            QueryArgument<BooleanGraphType> queryArgument3 = new QueryArgument<BooleanGraphType>();
            queryArgument3.Name = "latestVersion";
            queryArgument3.Description = "The item version to request (if not set, latest version is returned)";
            queryArgument3.DefaultValue = (object)true;
            queryArguments4.Add((QueryArgument)queryArgument3);

            QueryArguments queryArguments5 = queryArguments1;
            QueryArgument<StringGraphType> queryArgument4 = new QueryArgument<StringGraphType>();
            queryArgument4.Name = "index";
            queryArgument4.Description = "The search index name to query (defaults to the standard index for the current database)";
            queryArguments5.Add(queryArgument4);

            QueryArguments queryArguments6 = queryArguments1;
            QueryArgument<ListGraphType<CustomSearchQuery.CustomItemSearchFieldQueryValueGraphType>> queryArgument5 = new QueryArgument<ListGraphType<CustomSearchQuery.CustomItemSearchFieldQueryValueGraphType>>();
            queryArgument5.Name = "fieldsEqual";
            queryArgument5.Description = "Filter by index field value using equality (multiple fields are ANDed)";
            queryArguments6.Add(queryArgument5);

            this.Arguments = queryArguments1;

        }

        public Database Database { get; set; }

        protected override ContentSearchResults Resolve(ResolveFieldContext context)
        {
            string inputPathOrIdOrShortId = context.GetArgument<string>("rootItem");
            ID rootId = (ID)null;
            Item result1;
            if (!string.IsNullOrWhiteSpace(inputPathOrIdOrShortId) && IdHelper.TryResolveItem(this.Database, inputPathOrIdOrShortId, out result1))
                rootId = result1.ID;
            Language result2;
            if (!Language.TryParse(context.GetArgument<string>("language") ?? Context.Language.Name ?? LanguageManager.DefaultLanguage.Name, out result2))
                result2 = (Language)null;
            bool flag = context.GetArgument<bool>("latestVersion");
            string name1 = context.GetArgument<string>("index") ?? "sitecore_" + this.Database.Name.ToLowerInvariant() + "_index";
            IEnumerable<Dictionary<string, object>> dictionaries = context.GetArgument<object[]>("fieldsEqual", new object[0]).OfType<Dictionary<string, object>>();
            using (IProviderSearchContext searchContext = ContentSearchManager.GetIndex(name1).CreateSearchContext())
            {
                IQueryable<ContentSearchResult> queryable = searchContext.GetQueryable<ContentSearchResult>();
                if (rootId != (ID)null)
                    queryable = queryable.Where<ContentSearchResult>((Expression<Func<ContentSearchResult, bool>>)(result => result.AncestorIDs.Contains<ID>(rootId)));
                if (result2 != (Language)null)
                {
                    string resultLanguage = result2.Name;
                    queryable = queryable.Where<ContentSearchResult>((Expression<Func<ContentSearchResult, bool>>)(result => result.Language == resultLanguage));
                }
                if (flag)
                    queryable = queryable.Where<ContentSearchResult>((Expression<Func<ContentSearchResult, bool>>)(result => result.IsLatestVersion));
                foreach (Dictionary<string, object> dictionary in dictionaries)
                {
                    string name = dictionary["name"].ToString();
                    IEnumerable<string> value = ((List<object>)dictionary["value"]).Select(x => x.ToString());
                    string compoperator = dictionary["operator"].ToString();

                    if (compoperator.Equals("AND"))
                    {
                        var predicate = PredicateBuilder.True<ContentSearchResult>();
                        var aggregate = value.Aggregate(predicate, (current, f) => current.And(i => i[name].Contains(f)));
                        queryable = queryable.Where<ContentSearchResult>(aggregate);
                    }
                    else
                    {
                        var predicate = PredicateBuilder.False<ContentSearchResult>();
                        var aggregate = value.Aggregate(predicate, (current, f) => current.Or(i => i[name].Contains(f)));
                        queryable = queryable.Where<ContentSearchResult>(aggregate);
                    }
                }
                int? nullable = context.GetArgument<int?>("after");
                return new ContentSearchResults(queryable.ApplyEnumerableConnectionArguments<ContentSearchResult, object>((ResolveFieldContext<object>)context).GetResults<ContentSearchResult>(), nullable ?? 0);
            }
        }

        protected class CustomItemSearchFieldQueryValueGraphType : InputObjectGraphType
        {
            public CustomItemSearchFieldQueryValueGraphType()
            {
                this.Name = "CustomItemSearchFieldQuery";
                this.Field<NonNullGraphType<StringGraphType>>("name", "Index field name to filter on");
                this.Field<NonNullGraphType<ListGraphType<StringGraphType>>>("value", "Field value to filter on");
                this.Field<NonNullGraphType<StringGraphType>>("operator", "Operator to filter on");
            }
        }
    }
}
