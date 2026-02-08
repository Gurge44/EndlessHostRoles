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
    private event Action OnCanceled;
    
    private readonly bool _hasTickEvent;

    public CountdownTimer(float durationSeconds, Action onElapsed = null, bool autoStart = true, bool cancelOnMeeting = true, bool cancelOnGameEnd = true, Action onTick = null, Action onCanceled = null)
    {
        if (durationSeconds <= 0f)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));

        _durationSeconds = durationSeconds;
        _stopwatch = new Stopwatch();

        _cancelOnMeeting = cancelOnMeeting;
        _cancelOnGameEnd = cancelOnGameEnd;

        if (onElapsed != null)
            OnElapsed += onElapsed;

        _hasTickEvent = onTick != null;
        
        if (_hasTickEvent)
            OnTick += onTick;
        
        if (onCanceled != null)
            OnCanceled += onCanceled;
        
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

    public Stopwatch Stopwatch => _stopwatch;

    public void Start()
    {
        if (_coroutine != null)
            return;

        _completed = false;
        _stopwatch.Restart();
        _coroutine = Main.Instance.StartCoroutine(_hasTickEvent ? Run() : RunWithoutTicks());
    }

    private IEnumerator RunWithoutTicks()
    {
        yield return new WaitForSecondsRealtime(_durationSeconds);
        Complete();
    }

    private IEnumerator Run()
    {
        int lastRemaining = (int)Math.Ceiling(Remaining.TotalSeconds);

        while (_stopwatch.Elapsed.TotalSeconds < _durationSeconds)
        {
            int remaining = (int)Math.Ceiling(Remaining.TotalSeconds);

            if (lastRemaining != remaining)
            {
                if (IsCanceled()) break;
            
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

    public bool IsCanceled()
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

        try
        {
            if (!IsCanceled())
                OnElapsed?.Invoke();
            else
                OnCanceled?.Invoke();
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
        OnCanceled = null;
    }
}
