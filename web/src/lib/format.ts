const persianDigits = ['۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹']

export function toPersianDigits(value: string | number): string {
  return String(value).replace(/\d/g, (digit) => persianDigits[Number(digit)] ?? digit)
}

/** Prices are stored in Rial; the panel speaks Toman. */
export function formatToman(rial: number): string {
  const toman = Math.round(rial / 10)
  const grouped = toman.toLocaleString('en-US').replace(/,/g, '٬')
  return `${toPersianDigits(grouped)} تومان`
}

export function formatMinutes(minutes: number): string {
  if (minutes < 60) {
    return `${toPersianDigits(minutes)} دقیقه`
  }

  const hours = Math.floor(minutes / 60)
  const rest = minutes % 60

  return rest === 0
    ? `${toPersianDigits(hours)} ساعت`
    : `${toPersianDigits(hours)} ساعت و ${toPersianDigits(rest)} دقیقه`
}
