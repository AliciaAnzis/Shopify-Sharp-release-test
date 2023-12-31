﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace ShopifySharp
{
    internal class MultiShopifyAPIBucket
    {
        private const int DEFAULT_REST_MAX_AVAILABLE = 40;
        private const int DEFAULT_REST_RESTORE_RATE = 2;

        private const int DEFAULT_GRAPHQL_MAX_AVAILABLE = 1_000;
        private const int DEFAULT_GRAPHQL_RESTORE_RATE = 50;

        private LeakyBucket RESTBucket { get; }

        private LeakyBucket GraphQLBucket { get; }

        public MultiShopifyAPIBucket(Func<RequestContext> getRequestContext)
        {
            RESTBucket = new LeakyBucket(DEFAULT_REST_MAX_AVAILABLE, DEFAULT_REST_RESTORE_RATE, getRequestContext);
            GraphQLBucket = new LeakyBucket(DEFAULT_GRAPHQL_MAX_AVAILABLE, DEFAULT_GRAPHQL_RESTORE_RATE, getRequestContext);
        }

        public async Task WaitForAvailableRESTAsync(CancellationToken cancellationToken)
        {
            //REST calls all count for the same cost of 1
            await RESTBucket.WaitForAvailableAsync(1, cancellationToken);
        }

        public async Task WaitForAvailableGraphQLAsync(int queryCost, CancellationToken cancellationToken)
        {
            await GraphQLBucket.WaitForAvailableAsync(queryCost, cancellationToken);
        }

        public void SetRESTBucketState(int maximumAvailable, double currentlyAvailable)
        {
            //Shopify Plus customers have a bucket that is twice the size (80) so we resize the bucket capacity accordingly
            //It is apparently possible to request the bucket size to be even larger
            //https://ecommerce.shopify.com/c/shopify-apis-and-technology/t/what-is-the-default-api-call-limit-on-shopify-stores-407292
            //Note that when the capacity doubles, the leak rate also doubles. So, not only can request bursts be larger, it is also possible to sustain a faster rate over the long term.
            int restoreRatePerSecond = maximumAvailable / DEFAULT_REST_MAX_AVAILABLE * DEFAULT_REST_RESTORE_RATE;

            //Shopify might not have yet 'seen' requests in flight that were issued during the current request
            //i.e. multiple requests can receive their response out of order, causing the latest response bucket information to be incorrect (it is stale)
            currentlyAvailable = Math.Min(RESTBucket.ComputedCurrentlyAvailable, currentlyAvailable);

            RESTBucket.SetState(maximumAvailable, restoreRatePerSecond, currentlyAvailable);
        }

        public void SetGraphQLBucketState(int maximumAvailable, int restoreRatePerSecond, double currentlyAvailable, int refund)
        {
            currentlyAvailable = Math.Max(0, Math.Min(currentlyAvailable, GraphQLBucket.ComputedCurrentlyAvailable + refund));
            GraphQLBucket.SetState(maximumAvailable, restoreRatePerSecond, currentlyAvailable);
        }
    }
}
