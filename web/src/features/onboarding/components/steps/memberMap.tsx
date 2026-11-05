import { StepProfile, StepTour } from './MemberSteps'

/** Definições dos passos do convidado (separadas para o arquivo principal ficar legível). */
export const MemberStepsMap = {
  profile: {
    title: 'Seu perfil',
    description: 'Só o essencial para a equipe te reconhecer.',
    help: { title: 'Por que pedimos isto', body: <p>O nome aparece nas decisões registradas. A OAB é opcional e só faz sentido para quem advoga.</p> },
    Component: StepProfile,
  },
  tour: {
    title: 'O que importa para você',
    description: 'Um resumo rápido do que o seu perfil acessa.',
    help: { title: 'Cada perfil vê o que precisa', body: <p>Você enxerga só as áreas ligadas ao seu trabalho. Se faltar algo, peça ao sócio responsável.</p> },
    Component: StepTour,
  },
}
