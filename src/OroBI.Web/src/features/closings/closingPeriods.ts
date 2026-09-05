export function closingPeriodLabel(value: string) {
  if (!/^\d{4}-(0[1-9]|1[0-2])$/.test(value)) return 'Selecione o período'
  const [year, month] = value.split('-').map(Number)
  const label = new Intl.DateTimeFormat('pt-BR', { month: 'long', year: 'numeric' }).format(new Date(year, month - 1, 1))
  return label.charAt(0).toUpperCase() + label.slice(1)
}

export function closingPeriods(initialMonth: string) {
  const months = Array.from({ length: 24 }, (_, index) => {
    const date = new Date()
    date.setDate(1)
    date.setMonth(date.getMonth() - index)
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`
  })
  if (initialMonth && !months.includes(initialMonth)) months.push(initialMonth)
  return months.sort().reverse().map(value => ({ value, label: closingPeriodLabel(value) }))
}
