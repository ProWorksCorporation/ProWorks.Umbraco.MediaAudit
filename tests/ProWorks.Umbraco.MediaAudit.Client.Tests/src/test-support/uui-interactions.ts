export async function clickUuiCheckbox(checkbox: Element): Promise<void> {
  await (checkbox as unknown as { click(): Promise<void> }).click();
}

export async function clickUuiButton(button: Element): Promise<void> {
  await (button as unknown as { click(): Promise<void> }).click();
}
