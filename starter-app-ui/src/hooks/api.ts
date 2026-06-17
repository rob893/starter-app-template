import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { helloApi, notesApi } from '../services/api';
import { authApi } from '../services/auth';
import { useAuth } from './useAuth';
import type { LoginRequest, RegisterRequest } from '../types/auth';
import type {
  CreateNoteRequest,
  UpdateNoteRequest,
  Note,
  CursorPaginationQueryParameters,
  CursorPaginatedResponse
} from '../types/models';

export const queryKeys = {
  helloV1: ['hello', 'v1'] as const,
  helloV2: ['hello', 'v2'] as const,
  notes: (params?: CursorPaginationQueryParameters) => ['notes', params] as const,
  note: (id: number) => ['notes', id] as const
} as const;

// Auth hooks
export function useLogin() {
  return useMutation({
    mutationFn: (credentials: Omit<LoginRequest, 'deviceId'>) => authApi.login(credentials)
  });
}

export function useRegister() {
  return useMutation({
    mutationFn: (userData: Omit<RegisterRequest, 'deviceId'>) => authApi.register(userData)
  });
}

export function useLogout() {
  return useMutation({
    mutationFn: () => authApi.logout()
  });
}

export function useRefreshToken() {
  return useMutation({
    mutationFn: () => authApi.refreshToken()
  });
}

// Hello hooks
export function useHelloV1() {
  const { isLoading: isAuthLoading } = useAuth();

  return useQuery({
    queryKey: queryKeys.helloV1,
    queryFn: () => helloApi.getHelloV1(),
    enabled: !isAuthLoading,
    staleTime: 60 * 1000
  });
}

export function useHelloV2() {
  const { isLoading: isAuthLoading } = useAuth();

  return useQuery({
    queryKey: queryKeys.helloV2,
    queryFn: () => helloApi.getHelloV2(),
    enabled: !isAuthLoading,
    staleTime: 60 * 1000
  });
}

// Notes hooks
export function useNotes(params?: CursorPaginationQueryParameters) {
  const { isLoading: isAuthLoading, isAuthenticated } = useAuth();

  return useQuery({
    queryKey: queryKeys.notes(params),
    queryFn: () => notesApi.getNotes(params),
    enabled: isAuthenticated && !isAuthLoading,
    staleTime: 5 * 60 * 1000
  });
}

export function useNotesPage(cursor?: string, pageSize = 10) {
  return useNotes({ first: pageSize, after: cursor });
}

export function useCreateNote() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: CreateNoteRequest) => notesApi.createNote(data),
    onSuccess: newNote => {
      queryClient.invalidateQueries({ queryKey: ['notes'] });
      queryClient.setQueryData(queryKeys.note(newNote.id), newNote);
    }
  });
}

export function useUpdateNote() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }: { id: number; data: UpdateNoteRequest }) => notesApi.updateNote(id, data),
    onSuccess: updatedNote => {
      queryClient.invalidateQueries({ queryKey: ['notes'] });
      queryClient.setQueryData(queryKeys.note(updatedNote.id), updatedNote);
    }
  });
}

export function useDeleteNote() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: number) => notesApi.deleteNote(id),
    onSuccess: (_data, id) => {
      queryClient.invalidateQueries({ queryKey: ['notes'] });
      queryClient.removeQueries({ queryKey: queryKeys.note(id) });
    }
  });
}

// Cursor-paginated notes with "load more" support
export function useNotesCursorPaginated(pageSize = 10) {
  const { isLoading: isAuthLoading, isAuthenticated } = useAuth();
  const queryClient = useQueryClient();

  const firstPage = useQuery({
    queryKey: queryKeys.notes({ first: pageSize }),
    queryFn: () => notesApi.getNotes({ first: pageSize }),
    enabled: isAuthenticated && !isAuthLoading,
    staleTime: 5 * 60 * 1000
  });

  const loadMore = async (endCursor: string): Promise<CursorPaginatedResponse<Note>> => {
    const params = { first: pageSize, after: endCursor };
    const result = await notesApi.getNotes(params);
    queryClient.setQueryData(queryKeys.notes(params), result);
    return result;
  };

  return { firstPage, loadMore };
}
