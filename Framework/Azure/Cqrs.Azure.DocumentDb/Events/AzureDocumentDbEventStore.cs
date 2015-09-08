﻿#region Copyright
// // -----------------------------------------------------------------------
// // <copyright company="cdmdotnet Limited">
// // 	Copyright cdmdotnet Limited. All rights reserved.
// // </copyright>
// // -----------------------------------------------------------------------
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Cqrs.Events;
using cdmdotnet.Logging;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Linq;

namespace Cqrs.Azure.DocumentDb.Events
{
	public class AzureDocumentDbEventStore<TAuthenticationToken> : EventStore<TAuthenticationToken>
	{
		protected IAzureDocumentDbEventStoreConnectionStringFactory AzureDocumentDbEventStoreConnectionStringFactory { get; private set; }

		protected IAzureDocumentDbHelper AzureDocumentDbHelper { get; private set; }

		public AzureDocumentDbEventStore(IEventBuilder<TAuthenticationToken> eventBuilder, IEventDeserialiser<TAuthenticationToken> eventDeserialiser, ILogger logger, IAzureDocumentDbHelper azureDocumentDbHelper, IAzureDocumentDbEventStoreConnectionStringFactory azureDocumentDbEventStoreConnectionStringFactory)
			: base(eventBuilder, eventDeserialiser, logger)
		{
			AzureDocumentDbHelper = azureDocumentDbHelper;
			AzureDocumentDbEventStoreConnectionStringFactory = azureDocumentDbEventStoreConnectionStringFactory;
		}

		public override IEnumerable<IEvent<TAuthenticationToken>> Get<T>(Guid aggregateId, bool useLastEventOnly = false, int fromVersion = -1)
		{
			return GetAsync<T>(aggregateId, useLastEventOnly, fromVersion).Result;
		}

		protected async Task<IEnumerable<IEvent<TAuthenticationToken>>> GetAsync<T>(Guid aggregateId, bool useLastEventOnly = false, int fromVersion = -1)
		{
			using (DocumentClient client = AzureDocumentDbEventStoreConnectionStringFactory.GetEventStoreConnectionClient())
			{
				Database database = AzureDocumentDbHelper.CreateOrReadDatabase(client, AzureDocumentDbEventStoreConnectionStringFactory.GetEventStoreConnectionDatabaseName()).Result;
				//DocumentCollection collection = AzureDocumentDbHelper.CreateOrReadCollection(client, database, string.Format("{0}_{1}", AzureDocumentDbEventStoreConnectionStringFactory.GetEventStoreConnectionCollectionName(), typeof(T).FullName)).Result;
				DocumentCollection collection = AzureDocumentDbHelper.CreateOrReadCollection(client, database, string.Format("{0}::{1}", AzureDocumentDbEventStoreConnectionStringFactory.GetEventStoreConnectionCollectionName(), typeof(T).FullName)).Result;

				IOrderedQueryable<EventData> query = client.CreateDocumentQuery<EventData>(collection.SelfLink);
				string streamName = string.Format(CqrsEventStoreStreamNamePattern, typeof(T).FullName, aggregateId);

				IEnumerable<EventData> results = query.Where(x => x.AggregateId == streamName);

				return AzureDocumentDbHelper.ExecuteFaultTollerantFunction(() =>
					results
						.ToList()
						.OrderByDescending(x => x.EventId)
						.Select(EventDeserialiser.Deserialise)
				);
			}
		}

		protected override void PersitEvent(EventData eventData)
		{
			Logger.LogDebug("Persisting aggregate root event", string.Format("{0}\\PersitEvent", GetType().Name));
			try
			{
				using (DocumentClient client = AzureDocumentDbEventStoreConnectionStringFactory.GetEventStoreConnectionClient())
				{
					Database database = AzureDocumentDbHelper.CreateOrReadDatabase(client, AzureDocumentDbEventStoreConnectionStringFactory.GetEventStoreConnectionDatabaseName()).Result;
					//DocumentCollection collection = AzureDocumentDbHelper.CreateOrReadCollection(client, database, string.Format("{0}_{1}", AzureDocumentDbEventStoreConnectionStringFactory.GetEventStoreConnectionCollectionName(), eventData.EventType)).Result;
					DocumentCollection collection = AzureDocumentDbHelper.CreateOrReadCollection(client, database, string.Format("{0}::{1}", AzureDocumentDbEventStoreConnectionStringFactory.GetEventStoreConnectionCollectionName(), eventData.AggregateId.Substring(0, eventData.AggregateId.IndexOf("/", StringComparison.Ordinal)))).Result;

					Logger.LogDebug("Creating document for event asynchronously", string.Format("{0}\\PersitEvent", GetType().Name));
					AzureDocumentDbHelper.ExecuteFaultTollerantFunction(() => client.CreateDocumentAsync(collection.SelfLink, eventData).Wait());
				}
			}
			finally
			{
				Logger.LogDebug("Persisting aggregate root event... Done", string.Format("{0}\\PersitEvent", GetType().Name));
			}
		}
	}
}