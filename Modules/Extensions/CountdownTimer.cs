using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;

namespace EHR.Modules.Extensions;

public sealed class CountdownTimer : IDisposable
{
    private readonly float _durationSeconds;
    private readonly Stopwatch _stopwatch;
    private Coroutine _coroutine;
    private bool _completed;

    private readonly bool _cancelOnMeeting;
    private readonly bool _cancelOnGameEnd;

    private event Action OnElapsed;
    private event Action OnTick;

    public CountdownTimer(float durationSeconds, Action onElapsed = null, bool autoStart = true, bool cancelOnMeeting = true, bool cancelOnGameEnd = true, Action onTick = null)
    {
        if (durationSeconds <= 0f)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));

        _durationSeconds = durationSeconds;
        _stopwatch = new Stopwatch();

        _cancelOnMeeting = cancelOnMeeting;
        _cancelOnGameEnd = cancelOnGameEnd;

        if (onElapsed != null)
            OnElapsed += onElapsed;
        
        if (onTick != null)
            OnTick += onTick;
        
        if (autoStart)
            Start();
    }

    /// <summary>
    /// Remaining time (clamped to 0)
    /// </summary>
    public TimeSpan Remaining
    {
        get
        {
            if (_completed)
                return TimeSpan.Zero;

            TimeSpan remaining = TimeSpan.FromSeconds(_durationSeconds) - _stopwatch.Elapsed;
            return remaining >= TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public bool IsRunning => _stopwatch.IsRunning;
    public bool IsCompleted => _completed;

    public void Start()
    {
        if (_coroutine != null)
            return;

        _completed = false;
        _stopwatch.Restart();
        _coroutine = Main.Instance.StartCoroutine(Run());
    }

    public void Stop()
    {
        Dispose();
    }

    private IEnumerator Run()
    {
        int lastRemaining = (int)Math.Ceiling(Remaining.TotalSeconds);

        while (_stopwatch.Elapsed.TotalSeconds < _durationSeconds)
        {
            if (IsCanceled()) break;
            
            int remaining = (int)Math.Ceiling(Remaining.TotalSeconds);

            if (lastRemaining != remaining)
            {
                try { OnTick?.Invoke(); }
                catch (Exception e) { Utils.ThrowException(e); }
                
                lastRemaining = remaining;
            }

            if (remaining > 1)
                yield return new WaitForSecondsRealtime(1f);
            else
                yield return new WaitForSecondsRealtime((float)(_durationSeconds - _stopwatch.Elapsed.TotalSeconds));
        }

        Complete();
    }

    private bool IsCanceled()
    {
        if (_cancelOnMeeting && (ReportDeadBodyPatch.MeetingStarted || GameStates.IsMeeting || ExileController.Instance || AntiBlackout.SkipTasks)) return true;
        return _cancelOnGameEnd && (GameStates.IsEnded || GameStates.IsLobby || !GameStates.InGame);
    }

    private void Complete()
    {
        if (_completed)
            return;

        _completed = true;
        _stopwatch.Stop();

        try { OnTick?.Invoke(); }
        catch (Exception e) { Utils.ThrowException(e); }

        try
        {
            if (!IsCanceled())
                OnElapsed?.Invoke();
        }
        catch (Exception e)
        {
            Utils.ThrowException(e);
        }
        finally
        {
            Dispose();
        }
    }

    public void Dispose()
    {
        if (_coroutine != null)
        {
            Main.Instance.StopCoroutine(_coroutine);
            _coroutine = null;
        }

        _stopwatch.Stop();
        OnElapsed = null;
        OnTick = null;
    }
}
