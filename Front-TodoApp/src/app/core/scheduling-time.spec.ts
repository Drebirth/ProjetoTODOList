import { convertLocalWindow, formatUtcWindowInput, formatWindowInTimeZone, localInputToUtcIso, maxRecurrenceDuration, validateRecurrenceDuration } from './scheduling-time';

describe('scheduling time utilities', () => {
  it('converts civil time in the selected IANA zone to UTC and back without using the host zone', () => {
    expect(localInputToUtcIso('2026-11-09T09:00', 'America/Fortaleza')).toBe('2026-11-09T12:00:00.000Z');
    expect(formatUtcWindowInput('2026-11-09T12:00:00Z', 'America/Fortaleza')).toBe('2026-11-09T09:00');
    expect(formatWindowInTimeZone('2026-11-09T12:00:00Z', 'America/Fortaleza')).toContain('09:00');
  });

  it('advances a nonexistent local time by the DST gap and chooses the first ambiguous occurrence', () => {
    expect(localInputToUtcIso('2026-03-08T02:30', 'America/New_York')).toBe('2026-03-08T07:30:00.000Z');
    expect(localInputToUtcIso('2026-11-01T01:30', 'America/New_York')).toBe('2026-11-01T05:30:00.000Z');
  });

  it('validates a complete same-local-date window and converts both bounds as a pair', () => {
    expect(convertLocalWindow('', '', 'America/Fortaleza')).toEqual({ dataInicioUtc: null, dataFimUtc: null });
    expect(() => convertLocalWindow('2026-11-09T09:00', '', 'America/Fortaleza')).toThrow(/informadas juntas/i);
    expect(() => convertLocalWindow('2026-11-09T10:00', '2026-11-09T09:00', 'America/Fortaleza')).toThrow(/posterior/i);
    expect(() => convertLocalWindow('2026-11-09T23:30', '2026-11-10T00:30', 'America/Fortaleza')).toThrow(/mesma data local/i);
    expect(convertLocalWindow('2026-11-09T09:00', '2026-11-09T10:00', 'America/Fortaleza')).toEqual({ dataInicioUtc: '2026-11-09T12:00:00.000Z', dataFimUtc: '2026-11-09T13:00:00.000Z' });
  });

  it('uses the documented maximum recurrence duration per frequency', () => {
    expect(maxRecurrenceDuration('Diaria')).toBe(3650);
    expect(maxRecurrenceDuration('Semanal')).toBe(520);
    expect(maxRecurrenceDuration('Mensal')).toBe(120);
    expect(validateRecurrenceDuration('Diaria', 3650)).toBeNull();
    expect(validateRecurrenceDuration('Diaria', 3651)).toContain('3650');
    expect(validateRecurrenceDuration('Semanal', 521)).toContain('520');
    expect(validateRecurrenceDuration('Mensal', 121)).toContain('120');
    expect(validateRecurrenceDuration('Mensal', 0)).toContain('maior que zero');
  });
});
