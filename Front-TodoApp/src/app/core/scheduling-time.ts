import { RecurrenceFrequency } from '../models/todo.models';

interface CivilDateTime {
  year: number;
  month: number;
  day: number;
  hour: number;
  minute: number;
  second: number;
}

function parseCivilInput(value: string): CivilDateTime {
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?$/.exec(value);
  if (!match) throw new Error('Informe uma data e hora local válidas.');
  const [, yearText, monthText, dayText, hourText, minuteText, secondText = '0'] = match;
  const civil = {
    year: Number(yearText), month: Number(monthText), day: Number(dayText),
    hour: Number(hourText), minute: Number(minuteText), second: Number(secondText),
  };
  const check = new Date(Date.UTC(civil.year, civil.month - 1, civil.day, civil.hour, civil.minute, civil.second));
  if (check.getUTCFullYear() !== civil.year || check.getUTCMonth() !== civil.month - 1 || check.getUTCDate() !== civil.day
    || civil.hour > 23 || civil.minute > 59 || civil.second > 59) throw new Error('Informe uma data e hora local válidas.');
  return civil;
}

function civilText(civil: CivilDateTime): string {
  const pad = (part: number) => String(part).padStart(2, '0');
  return `${civil.year}-${pad(civil.month)}-${pad(civil.day)}T${pad(civil.hour)}:${pad(civil.minute)}`;
}

function partsAt(epochMs: number, timeZoneId: string): CivilDateTime {
  const formatter = new Intl.DateTimeFormat('en-GB', {
    timeZone: timeZoneId, year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23',
  });
  const parts = Object.fromEntries(formatter.formatToParts(new Date(epochMs)).map(({ type, value }) => [type, value]));
  return {
    year: Number(parts['year']), month: Number(parts['month']), day: Number(parts['day']),
    hour: Number(parts['hour']), minute: Number(parts['minute']), second: Number(parts['second']),
  };
}

function offsetAt(epochMs: number, timeZoneId: string): number {
  const local = partsAt(epochMs, timeZoneId);
  const localAsUtc = Date.UTC(local.year, local.month - 1, local.day, local.hour, local.minute, local.second);
  return (localAsUtc - Math.floor(epochMs / 1000) * 1000) / 60_000;
}

export function localInputToUtcIso(value: string, timeZoneId: string): string {
  const civil = parseCivilInput(value);
  const wallTimeMs = Date.UTC(civil.year, civil.month - 1, civil.day, civil.hour, civil.minute, civil.second);
  const offsets = new Set<number>();
  for (let hours = -36; hours <= 36; hours += 3) offsets.add(offsetAt(wallTimeMs + hours * 3_600_000, timeZoneId));
  const candidates = [...offsets]
    .map((offset) => wallTimeMs - offset * 60_000)
    .filter((candidate) => civilText(partsAt(candidate, timeZoneId)) === civilText(civil))
    .sort((a, b) => a - b);
  if (candidates.length > 0) return new Date(candidates[0]).toISOString();

  // Resolving a skipped wall time with the earlier offset advances it by the DST gap.
  const earlierOffset = Math.min(...offsets);
  return new Date(wallTimeMs - earlierOffset * 60_000).toISOString();
}

export function convertLocalWindow(startLocal: string, endLocal: string, timeZoneId: string): { dataInicioUtc: string | null; dataFimUtc: string | null } {
  const start = startLocal.trim();
  const end = endLocal.trim();
  if (!start && !end) return { dataInicioUtc: null, dataFimUtc: null };
  if (!start || !end) throw new Error('Início e fim devem ser informados juntos.');
  if (start.slice(0, 10) !== end.slice(0, 10)) throw new Error('Início e fim devem estar na mesma data local.');
  const startUtc = localInputToUtcIso(start, timeZoneId);
  const endUtc = localInputToUtcIso(end, timeZoneId);
  if (Date.parse(endUtc) <= Date.parse(startUtc)) throw new Error('O fim deve ser posterior ao início.');
  return { dataInicioUtc: startUtc, dataFimUtc: endUtc };
}

export function formatUtcWindowInput(instantUtc: string, timeZoneId: string): string {
  return civilText(partsAt(Date.parse(instantUtc), timeZoneId));
}

export function formatWindowInTimeZone(instantUtc: string, timeZoneId: string): string {
  return new Intl.DateTimeFormat('pt-BR', {
    timeZone: timeZoneId, dateStyle: 'medium', hour: '2-digit', minute: '2-digit', hourCycle: 'h23',
  }).format(new Date(instantUtc));
}

export function maxRecurrenceDuration(frequency: RecurrenceFrequency): number {
  switch (frequency) {
    case 'Diaria': return 3650;
    case 'Semanal': return 520;
    case 'Mensal': return 120;
  }
}

export function validateRecurrenceDuration(frequency: RecurrenceFrequency, duration: number): string | null {
  if (!Number.isInteger(duration) || duration < 1) return 'A duração da recorrência deve ser um inteiro maior que zero.';
  const maximum = maxRecurrenceDuration(frequency);
  return duration > maximum ? `A duração máxima para recorrência ${frequency.toLowerCase()} é ${maximum}.` : null;
}
