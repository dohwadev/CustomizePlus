﻿// © Customize+.
// Licensed under the MIT license.

using CustomizePlus.Core;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomizePlus.Helpers;

namespace CustomizePlus.Services
{
	//Borrowed from Brio
	internal class GPoseService : ServiceBase<GPoseService>
	{
		public GPoseState GPoseState { get; private set; }
		public bool IsInGPose => GPoseState == GPoseState.Inside;
		public bool FakeGPose
		{
			get => _fakeGPose;

			set
			{
				if (value != _fakeGPose)
				{
					if (!value)
					{
						_fakeGPose = false;
						HandleGPoseChange(GPoseState.Exiting);
						HandleGPoseChange(GPoseState.Outside);
					}
					else
					{
						HandleGPoseChange(GPoseState.Inside);
						_fakeGPose = true;
					}
				}
			}
		}

		private bool _fakeGPose = false;

		public delegate void OnGPoseStateDelegate(GPoseState gposeState);
		public event OnGPoseStateDelegate? OnGPoseStateChange;

		private delegate void ExitGPoseDelegate(IntPtr addr);
		private Hook<ExitGPoseDelegate>? _exitGPoseHook = null;

		private delegate bool EnterGPoseDelegate(IntPtr addr);
		private Hook<EnterGPoseDelegate>? _enterGPoseHook = null;

		public override unsafe void Start()
		{
			GPoseState = DalamudServices.PluginInterface.UiBuilder.GposeActive ? GPoseState.Inside : GPoseState.Outside;

			UIModule* uiModule = Framework.Instance()->GetUiModule();
			var enterGPoseAddress = (nint)uiModule->VTable->EnterGPose;
			var exitGPoseAddress = (nint)uiModule->VTable->ExitGPose;

			_enterGPoseHook = Hook<EnterGPoseDelegate>.FromAddress(enterGPoseAddress, EnteringGPoseDetour);
			_enterGPoseHook.Enable();

			_exitGPoseHook = Hook<ExitGPoseDelegate>.FromAddress(exitGPoseAddress, ExitingGPoseDetour);
			_exitGPoseHook.Enable();

			base.Start();
		}

		private void ExitingGPoseDetour(IntPtr addr)
		{
			if (HandleGPoseChange(GPoseState.AttemptExit))
			{
				HandleGPoseChange(GPoseState.Exiting);
				_exitGPoseHook!.Original.Invoke(addr);
				HandleGPoseChange(GPoseState.Outside);
			}
		}

		private bool EnteringGPoseDetour(IntPtr addr)
		{
			bool didEnter = _enterGPoseHook!.Original.Invoke(addr);
			if (didEnter)
			{
				_fakeGPose = false;
				HandleGPoseChange(GPoseState.Inside);
			}

			return didEnter;
		}

		private bool HandleGPoseChange(GPoseState state)
		{
			if (state == GPoseState || _fakeGPose)
				return true;

			GPoseState = state;

			try
			{
				OnGPoseStateChange?.Invoke(state);
			}
			catch (Exception e)
			{
				ChatHelper.PrintInChat($"GPose transition error.\n Reason: {e.Message}");
				return false;
			}

			return true;
		}

		public override void Dispose()
		{
			_exitGPoseHook?.Dispose();
			_enterGPoseHook?.Dispose();
		}
	}

	public enum GPoseState
	{
		Inside,
		AttemptExit,
		Exiting,
		Outside
	}
}