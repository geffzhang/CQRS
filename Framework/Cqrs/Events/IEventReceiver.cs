﻿#region Copyright
// // -----------------------------------------------------------------------
// // <copyright company="cdmdotnet Limited">
// // 	Copyright cdmdotnet Limited. All rights reserved.
// // </copyright>
// // -----------------------------------------------------------------------
#endregion

namespace Cqrs.Events
{
	public interface IEventReceiver
	{
		void Start();
	}

	public interface IEventReceiver<TAuthenticationToken>
		: IEventReceiver
	{
		void ReceiveEvent(IEvent<TAuthenticationToken> @event);
	}
}