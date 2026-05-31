from __future__ import annotations

import argparse
import json
from dataclasses import dataclass
from datetime import date, datetime, timedelta
from typing import Iterable


TARGETS = {"highmarshal", "archivist"}


@dataclass(frozen=True)
class MarketDayDecision:
    target: str
    local_date: str
    run: bool
    kind: str
    reason: str


def _nth_weekday(year: int, month: int, weekday: int, occurrence: int) -> date:
    current = date(year, month, 1)
    while current.weekday() != weekday:
        current += timedelta(days=1)
    return current + timedelta(days=7 * (occurrence - 1))


def _last_weekday(year: int, month: int, weekday: int) -> date:
    if month == 12:
        current = date(year + 1, 1, 1) - timedelta(days=1)
    else:
        current = date(year, month + 1, 1) - timedelta(days=1)
    while current.weekday() != weekday:
        current -= timedelta(days=1)
    return current


def _observed_fixed_holiday(year: int, month: int, day: int) -> date:
    actual = date(year, month, day)
    if actual.weekday() == 5:
        return actual - timedelta(days=1)
    if actual.weekday() == 6:
        return actual + timedelta(days=1)
    return actual


def _easter_sunday(year: int) -> date:
    a = year % 19
    b = year // 100
    c = year % 100
    d = b // 4
    e = b % 4
    f = (b + 8) // 25
    g = (b - f + 1) // 3
    h = (19 * a + b - d - g + 15) % 30
    i = c // 4
    k = c % 4
    l = (32 + 2 * e + 2 * i - h - k) % 7
    m = (a + 11 * h + 22 * l) // 451
    month = (h + l - 7 * m + 114) // 31
    day = ((h + l - 7 * m + 114) % 31) + 1
    return date(year, month, day)


def _good_friday(year: int) -> date:
    return _easter_sunday(year) - timedelta(days=2)


def us_stock_market_holidays(year: int) -> dict[date, str]:
    return {
        _observed_fixed_holiday(year, 1, 1): "New Year's Day",
        _nth_weekday(year, 1, 0, 3): "Martin Luther King Jr. Day",
        _nth_weekday(year, 2, 0, 3): "Presidents' Day",
        _good_friday(year): "Good Friday",
        _last_weekday(year, 5, 0): "Memorial Day",
        _observed_fixed_holiday(year, 6, 19): "Juneteenth",
        _observed_fixed_holiday(year, 7, 4): "Independence Day",
        _nth_weekday(year, 9, 0, 1): "Labor Day",
        _nth_weekday(year, 11, 3, 4): "Thanksgiving Day",
        _observed_fixed_holiday(year, 12, 25): "Christmas Day",
    }


def _holiday_entries_for(date_value: date) -> Iterable[tuple[date, str]]:
    for year in (date_value.year - 1, date_value.year, date_value.year + 1):
        for holiday_date, holiday_name in us_stock_market_holidays(year).items():
            yield holiday_date, holiday_name


def decide_market_day(target: str, now: datetime | None = None) -> MarketDayDecision:
    normalized_target = target.strip().lower()
    if normalized_target not in TARGETS:
        raise ValueError(f"Unsupported target: {target}")

    local_now = now or datetime.now().astimezone()
    local_day = local_now.date()
    if local_day.weekday() >= 5:
        return MarketDayDecision(
            target=normalized_target,
            local_date=local_day.isoformat(),
            run=False,
            kind="weekend",
            reason="Weekend: US stock market is closed.",
        )

    holiday_map = dict(_holiday_entries_for(local_day))
    if local_day in holiday_map:
        return MarketDayDecision(
            target=normalized_target,
            local_date=local_day.isoformat(),
            run=False,
            kind="holiday",
            reason=f"US stock market holiday: {holiday_map[local_day]}.",
        )

    return MarketDayDecision(
        target=normalized_target,
        local_date=local_day.isoformat(),
        run=True,
        kind="trading_day",
        reason="Valid US stock market trading day.",
    )


def cron_gate_payload(target: str, now: datetime | None = None) -> dict[str, object]:
    decision = decide_market_day(target, now)
    return {
        "wakeAgent": decision.run,
        "context": {
            "target": decision.target,
            "local_date": decision.local_date,
            "run": decision.run,
            "kind": decision.kind,
            "reason": decision.reason,
        },
    }


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Determine whether a scheduled Hermes Desktop AlTrader agent should run today."
    )
    parser.add_argument("target", choices=sorted(TARGETS))
    parser.add_argument("--json", action="store_true", dest="emit_json")
    parser.add_argument("--cron-gate", action="store_true")
    args = parser.parse_args()

    if args.cron_gate:
        print(json.dumps(cron_gate_payload(args.target), indent=2))
        return

    decision = decide_market_day(args.target)
    payload = {
        "target": decision.target,
        "local_date": decision.local_date,
        "run": decision.run,
        "kind": decision.kind,
        "reason": decision.reason,
    }
    if args.emit_json:
        print(json.dumps(payload, indent=2))
    else:
        print(payload["reason"])


if __name__ == "__main__":
    main()
