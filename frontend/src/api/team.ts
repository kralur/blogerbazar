import { api } from "./client";

// D64 stage C1: a company's team. The owner (or an admin) makes a one-time link for a role; the person who opens it
// asks for access, and the owner or an admin sees their name and confirmed number and lets them in.

export type TeamRole = "owner" | "admin" | "manager" | "viewer";
export type InvitableRole = Exclude<TeamRole, "owner">;
export type TeamInvitationState = "open" | "requested" | "requestedByYou" | "joined" | "declined" | "used" | "expired";

export type TeamMember = { id: string; firstName: string; username?: string | null; role: TeamRole; joinedAtUtc: string; isMe: boolean };
export type TeamRequest = { id: string; firstName: string; username?: string | null; phone?: string | null; role: TeamRole; requestedAtUtc: string };
export type TeamInvitation = { id: string; role: TeamRole; expiresAtUtc: string };
export type Team = {
  companyId: string;
  companyName: string;
  teamMode: boolean;
  myRole: TeamRole;
  canManage: boolean;
  seats: number;
  seatsUsed: number;
  members: TeamMember[];
  requests: TeamRequest[];
  invitations: TeamInvitation[];
};
export type TeamInvitationLink = { id: string; role: TeamRole; expiresAtUtc: string; link?: string | null };
export type TeamInvitationPreview = { companyName: string; companyLogoUrl?: string | null; role: TeamRole; state: TeamInvitationState };

export const invitableRoles: InvitableRole[] = ["manager", "viewer", "admin"];

// The link that opens the Mini App with an invitation: t.me/<bot>?startapp=join_<code>.
export const teamJoinPrefix = "join_";

export function teamJoinCodeFrom(startParam: string | undefined | null) {
  if (!startParam?.startsWith(teamJoinPrefix)) return null;
  const code = startParam.slice(teamJoinPrefix.length);
  return /^[A-Za-z0-9_-]{16,64}$/.test(code) ? code : null;
}

// Only the owner invites admins; nobody is invited as the owner.
export function rolesYouMayGive(myRole: TeamRole): InvitableRole[] {
  return myRole === "owner" ? invitableRoles : invitableRoles.filter((role) => role !== "admin");
}

export function mayHandle(myRole: TeamRole, role: TeamRole) {
  return role !== "owner" && (role !== "admin" || myRole === "owner");
}

export const getTeam = () => api<Team>("/api/team");
export const setTeamMode = (teamMode: boolean) => api<Team>("/api/team/mode", { method: "PUT", body: JSON.stringify({ teamMode }) });
export const createTeamInvitation = (role: InvitableRole) => api<TeamInvitationLink>("/api/team/invitations", { method: "POST", body: JSON.stringify({ role }) });
export const revokeTeamInvitation = (id: string) => api<void>(`/api/team/invitations/${encodeURIComponent(id)}`, { method: "DELETE" });
export const previewTeamInvitation = (code: string) => api<TeamInvitationPreview>("/api/team/invitations/preview", { method: "POST", body: JSON.stringify({ code }) });
export const requestTeamAccess = (code: string) => api<TeamInvitationPreview>("/api/team/invitations/request", { method: "POST", body: JSON.stringify({ code }) });
export const acceptTeamRequest = (id: string) => api<Team>(`/api/team/requests/${encodeURIComponent(id)}/accept`, { method: "POST" });
export const declineTeamRequest = (id: string) => api<Team>(`/api/team/requests/${encodeURIComponent(id)}/decline`, { method: "POST" });
export const changeTeamRole = (id: string, role: InvitableRole) => api<Team>(`/api/team/members/${encodeURIComponent(id)}/role`, { method: "PUT", body: JSON.stringify({ role }) });
export const removeTeamMember = (id: string) => api<Team>(`/api/team/members/${encodeURIComponent(id)}`, { method: "DELETE" });
export const leaveTeam = () => api<void>("/api/team/leave", { method: "POST" });
export const sendWorkOnComputerLink = () => api<{ url: string }>("/api/team/web-link", { method: "POST" });

// "Work on a computer": the site is this app's own address; the bot also puts it into the chat.
export function siteAddress() {
  return typeof window === "undefined" ? "" : window.location.origin;
}
