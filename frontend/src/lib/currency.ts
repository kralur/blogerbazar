export const formatCurrency = (value?: number | null) => (value ? `${value.toLocaleString("ru-RU")} сум` : "по запросу");
