import { TextField, Input, Label, Description, FieldError } from '@heroui/react';

interface FormFieldProps {
  label?: string;
  name?: string;
  type?: string;
  value: string;
  onChange(value: string): void;
  isRequired?: boolean;
  isDisabled?: boolean;
  isInvalid?: boolean;
  placeholder?: string;
  description?: string;
  errorMessage?: string;
  autoComplete?: string;
  className?: string;
}

export function FormField({
  label,
  name,
  type = 'text',
  value,
  onChange,
  isRequired,
  isDisabled,
  isInvalid,
  placeholder,
  description,
  errorMessage,
  autoComplete,
  className
}: FormFieldProps) {
  return (
    <TextField
      name={name}
      type={type}
      value={value}
      onChange={onChange}
      isRequired={isRequired}
      isDisabled={isDisabled}
      isInvalid={isInvalid}
      className={className ?? 'w-full'}
    >
      {label && <Label>{label}</Label>}
      <Input placeholder={placeholder} autoComplete={autoComplete} className="w-full" />
      {description && !isInvalid && <Description>{description}</Description>}
      {isInvalid && errorMessage && <FieldError>{errorMessage}</FieldError>}
      {!isInvalid && !description && <FieldError />}
    </TextField>
  );
}
